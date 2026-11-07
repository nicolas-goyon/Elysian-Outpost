// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using ElysianOutpost.Core.Cameras;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// The GPU buffers of the chunk meshes: one shared vertex buffer and one shared index buffer for all the chunks (#52).
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="BinaryTreeBufferPartitioner"/> of BRUTAL gives each chunk one block of the vertex buffer and one block of the index buffer.
/// A block has a size of a power of two. A third buffer has the world position of each chunk, in a slot for each chunk.
/// </para>
/// <para>
/// <see cref="Record"/> binds the three buffers one time. Then one <c>vkCmdDrawIndexedIndirect</c> command draws all the chunks in the camera view.
/// Each indirect command gives the index range and the vertex offset of a chunk, and its slot as the first instance.
/// Each frame in flight has its own buffer of indirect commands. Without the features multiDrawIndirect and drawIndirectFirstInstance,
/// the method records one indexed draw for each chunk, with the same bound buffers.
/// </para>
/// <para>
/// <see cref="Upload"/> copies a group of chunks to the GPU through one staging buffer, then waits until the GPU completes the copy.
/// The wait also includes the frames in flight: the signal of a fence comes after all the earlier submissions to the queue
/// (Vulkan specification, "Fence Signaling"). After the wait, the GPU does not use the blocks and the slots of the replaced meshes,
/// and the upload frees them.
/// </para>
/// <para>
/// The buffers have a fixed capacity. If a chunk does not fit, <see cref="Upload"/> throws <see cref="InvalidOperationException"/>.
/// The sample scene uses blocks of 0.64 MiB for 0.32 MiB of mesh data: 8 % of the capacity.
/// </para>
/// </remarks>
internal sealed unsafe class ChunkMeshes : IDisposable
{
    /// <summary>
    /// The capacity of the shared vertex buffer and of the shared index buffer, in bytes. It must be a power of two.
    /// </summary>
    public const int SharedBufferBytes = 4 * 1024 * 1024;

    /// <summary>
    /// The maximum number of chunks with a mesh.
    /// </summary>
    public const int MaxChunks = 16384;

    // The smallest block of the partitioners. A block offset is a multiple of its size, thus a multiple of the vertex and the index size.
    private const int MinBlockBytes = 256;

    private readonly GpuContext _gpu;
    private readonly Dictionary<int3, GpuChunk> _chunks = [];
    private readonly List<GpuChunk> _replaced = [];
    private readonly Stack<int> _freeSlots = new();
    private readonly VkCommandPool _commandPool;
    private readonly CommandBuffer _commandBuffer;
    private readonly VkFence _fence;
    private readonly SharedBuffer _vertices;
    private readonly SharedBuffer _indices;
    private readonly MappedBuffer _positions;
    private readonly MappedBuffer[] _indirect;
    private readonly BinaryTreeBufferPartitioner _vertexBlocks = null!;
    private readonly BinaryTreeBufferPartitioner _indexBlocks = null!;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkMeshes"/> class, with no chunk.
    /// </summary>
    /// <param name="gpu">The Vulkan objects of the window.</param>
    /// <param name="framesInFlight">The number of frames in flight of the renderer. Each one has a buffer of indirect commands.</param>
    public ChunkMeshes(GpuContext gpu, int framesInFlight)
    {
        _gpu = gpu;
        _indirect = new MappedBuffer[framesInFlight];
        for (int slot = MaxChunks - 1; slot >= 0; slot--)
        {
            _freeSlots.Push(slot);
        }

        var poolInfo = new VkCommandPoolCreateInfo
        {
            Flags = VkCommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = gpu.QueueFamily,
        };
        _commandPool = gpu.Device.CreateCommandPool(in poolInfo, null);
        try
        {
            _commandBuffer = gpu.Device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo
            {
                CommandPool = _commandPool,
                Level = VkCommandBufferLevel.Primary,
            });
            var fenceInfo = new VkFenceCreateInfo();
            _fence = gpu.Device.CreateFence(in fenceInfo, null);
            _vertices = CreateDeviceBuffer(SharedBufferBytes, VkBufferUsageFlags.VertexBufferBit);
            _indices = CreateDeviceBuffer(SharedBufferBytes, VkBufferUsageFlags.IndexBufferBit);
            _vertexBlocks = CreatePartitioner(_vertices, VkBufferUsageFlags.VertexBufferBit);
            _indexBlocks = CreatePartitioner(_indices, VkBufferUsageFlags.IndexBufferBit);
            _positions = CreateMappedBuffer(MaxChunks * ChunkPipeline.ChunkPositionSize, VkBufferUsageFlags.VertexBufferBit);
            for (int i = 0; i < framesInFlight; i++)
            {
                _indirect[i] = CreateMappedBuffer(MaxChunks * sizeof(VkDrawIndexedIndirectCommand), VkBufferUsageFlags.IndirectBufferBit);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the number of chunks with a mesh on the GPU. A chunk with no quad has no mesh.
    /// </summary>
    public int ChunkCount => _chunks.Count;

    /// <summary>
    /// Gets the number of GPU buffers of the chunk meshes: the shared vertex and index buffers, the position buffer,
    /// and one indirect buffer for each frame in flight. The number does not change with the number of chunks (#52).
    /// </summary>
    public int BufferCount => 3 + _indirect.Length;

    /// <summary>
    /// Gets the size of the mesh data of the chunks, in bytes. The blocks of the shared buffers can be larger, because a block has a size of a power of two.
    /// </summary>
    public long BufferBytes { get; private set; }

    /// <summary>
    /// Gets the size of the blocks of the shared buffers that the chunks use, in bytes.
    /// </summary>
    public long BlockBytes { get; private set; }

    /// <summary>
    /// Gets a value that tells if <see cref="Record"/> draws all the chunks with one indirect command.
    /// </summary>
    public bool MultiDrawIndirect => _gpu.MultiDrawIndirect;

    /// <summary>
    /// Gets the total time of the uploads, from the creation of the staging buffer to the end of the copy on the GPU.
    /// </summary>
    public TimeSpan UploadTime { get; private set; }

    /// <summary>
    /// Copies the meshes of a group of chunks to the shared GPU buffers, and waits until the GPU completes the copy.
    /// If a chunk has a mesh, the new mesh replaces it, and the method frees the old blocks after the wait.
    /// </summary>
    /// <param name="chunks">
    /// The chunks. Each position must occur only one time. A chunk with no quad gets no mesh: the method only frees its old mesh.
    /// </param>
    /// <exception cref="ArgumentException">A position occurs two times in <paramref name="chunks"/>.</exception>
    /// <exception cref="InvalidOperationException">The shared buffers or the position slots have no space for a chunk.</exception>
    public void Upload(ReadOnlySpan<BuiltChunk> chunks)
    {
        long start = Stopwatch.GetTimestamp();
        long stagingSize = 0;
        bool replaces = false;
        for (int i = 0; i < chunks.Length; i++)
        {
            for (int j = 0; j < i; j++)
            {
                if (chunks[j].Position == chunks[i].Position)
                {
                    throw new ArgumentException($"The chunk at ({chunks[i].Position.X}, {chunks[i].Position.Y}, {chunks[i].Position.Z}) occurs two times.", nameof(chunks));
                }
            }

            stagingSize += VertexBytes(chunks[i].Mesh) + IndexBytes(chunks[i].Mesh);
            replaces |= _chunks.ContainsKey(chunks[i].Position);
        }

        if (stagingSize == 0 && !replaces)
        {
            return;
        }

        // With no quad to copy, the command buffer has only the barriers. Its fence still waits for the frames that use the old blocks.
        (VkBuffer staging, VmaAllocation stagingAllocation, nint mappedAddress) = stagingSize > 0
            ? CreateStagingBuffer(stagingSize)
            : (VkBuffer.NullHandle, default(VmaAllocation), 0);
        byte* mapped = (byte*)mappedAddress;
        try
        {
            _commandBuffer.Reset();
            _commandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);

            // The new blocks can use the memory of blocks that an earlier upload freed. The earlier frames read that memory.
            // The fence wait of the previous upload makes this safe, but the synchronization validation does not see a host wait.
            // Thus this execution barrier makes the dependency explicit: the copies wait for the vertex and index reads of the earlier submissions.
            var readsBeforeCopies = new VkMemoryBarrier2
            {
                SrcStageMask = VkPipelineStageFlags2.VertexAttributeInputBit | VkPipelineStageFlags2.IndexInputBit,
                DstStageMask = VkPipelineStageFlags2.CopyBit,
            };
            var readsDependency = new VkDependencyInfo
            {
                MemoryBarrierCount = 1,
                MemoryBarriers = &readsBeforeCopies,
            };
            _commandBuffer.PipelineBarrier2(in readsDependency);
            long offset = 0;
            foreach (BuiltChunk chunk in chunks)
            {
                if (_chunks.Remove(chunk.Position, out GpuChunk old))
                {
                    _replaced.Add(old);
                }

                if (chunk.Mesh.QuadCount > 0)
                {
                    offset = AddChunk(chunk, staging, mapped, offset);
                }
            }

            if (stagingSize > 0)
            {
                _gpu.Allocator.FlushAllocation(stagingAllocation, ByteSize.Zero, (ByteSize)stagingSize);
            }

            _gpu.Allocator.FlushAllocation(_positions.Allocation, ByteSize.Zero, (ByteSize)(MaxChunks * ChunkPipeline.ChunkPositionSize));

            // The draws of the next frames read the new blocks. They must wait for the copy.
            var barrier = new VkMemoryBarrier2
            {
                SrcStageMask = VkPipelineStageFlags2.CopyBit,
                SrcAccessMask = VkAccessFlags2.TransferWriteBit,
                DstStageMask = VkPipelineStageFlags2.VertexAttributeInputBit | VkPipelineStageFlags2.IndexInputBit,
                DstAccessMask = VkAccessFlags2.VertexAttributeReadBit | VkAccessFlags2.IndexReadBit,
            };
            var dependency = new VkDependencyInfo
            {
                MemoryBarrierCount = 1,
                MemoryBarriers = &barrier,
            };
            _commandBuffer.PipelineBarrier2(in dependency);
            _commandBuffer.End();

            var command = new VkCommandBufferSubmitInfo { CommandBuffer = _commandBuffer.Handle };
            var submit = new VkSubmitInfo2
            {
                CommandBufferInfoCount = 1,
                CommandBufferInfos = &command,
            };
            _gpu.Queue.Submit2(new Span<VkSubmitInfo2>(&submit, 1), _fence);
            _gpu.Device.WaitForFences([_fence], true, -1);
            _gpu.Device.ResetFences([_fence]);

            // The GPU completed all the frames that used the old blocks. If an exception occurs before this line,
            // the GPU can still use them. Then the next upload that completes frees them.
            // A chunk that an exception removed from the list has no mesh until its next upload.
            FreeReplacedChunks();
        }
        finally
        {
            if (stagingSize > 0)
            {
                _gpu.Allocator.DestroyBuffer(staging, stagingAllocation);
            }
        }

        UploadTime += Stopwatch.GetElapsedTime(start);
    }

    /// <summary>
    /// Records the draw commands of the chunks in the camera view. The pipeline must be bound, with the view-projection matrix in its push constants.
    /// </summary>
    /// <param name="commandBuffer">The command buffer, in a rendering scope.</param>
    /// <param name="frame">The index of the frame in flight. The fence of this frame must be signaled: the method writes its indirect buffer.</param>
    /// <param name="frustum">The planes of the camera view. The method skips a chunk whose box is outside them (#50).</param>
    /// <returns>The number of chunks that the method draws.</returns>
    public int Record(CommandBuffer commandBuffer, int frame, in ViewFrustum frustum)
    {
        MappedBuffer indirect = _indirect[frame];
        var commands = (VkDrawIndexedIndirectCommand*)indirect.Address;
        int drawn = 0;
        foreach (GpuChunk chunk in _chunks.Values)
        {
            // A chunk whose box is outside the camera view does not need a draw (#50).
            if (!frustum.Intersects(chunk.BoundsMin, chunk.BoundsMax))
            {
                continue;
            }

            commands[drawn++] = new VkDrawIndexedIndirectCommand
            {
                IndexCount = chunk.IndexCount,
                InstanceCount = 1,
                FirstIndex = chunk.FirstIndex,
                VertexOffset = chunk.VertexOffset,
                FirstInstance = chunk.Slot,
            };
        }

        if (drawn == 0)
        {
            return 0;
        }

        // The terrain buffers are bound one time in each frame (#52).
        commandBuffer.BindVertexBuffers(0, [_vertices.Buffer, _positions.Buffer], [default(ByteSize64), default(ByteSize64)]);
        commandBuffer.BindIndexBuffer(_indices.Buffer, default, VkIndexType.UInt32);
        if (_gpu.MultiDrawIndirect)
        {
            _gpu.Allocator.FlushAllocation(indirect.Allocation, ByteSize.Zero, (ByteSize)(drawn * sizeof(VkDrawIndexedIndirectCommand)));
            commandBuffer.DrawIndexedIndirect(indirect.Buffer, default, drawn, (ByteSize32)sizeof(VkDrawIndexedIndirectCommand));
        }
        else
        {
            // A direct draw can always give a first instance.
            for (int i = 0; i < drawn; i++)
            {
                VkDrawIndexedIndirectCommand c = commands[i];
                commandBuffer.DrawIndexed(c.IndexCount, 1, c.FirstIndex, c.VertexOffset, c.FirstInstance);
            }
        }

        return drawn;
    }

    /// <summary>
    /// Releases all the buffers, the fence and the command pool. The GPU must not use them.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _chunks.Clear();
        _replaced.Clear();
        _vertices.Destroy(_gpu);
        _indices.Destroy(_gpu);
        _positions.Destroy(_gpu);
        foreach (MappedBuffer buffer in _indirect)
        {
            buffer.Destroy(_gpu);
        }

        if (!_fence.IsNull())
        {
            _gpu.Device.DestroyFence(_fence, null);
        }

        // The destruction of the command pool also frees its command buffer.
        if (!_commandPool.IsNull())
        {
            _gpu.Device.DestroyCommandPool(_commandPool, null);
        }
    }

    private static long VertexBytes(ChunkMeshData mesh) => (long)mesh.Vertices.Length * ChunkVertex.SizeInBytes;

    private static long IndexBytes(ChunkMeshData mesh) => (long)mesh.Indices.Length * sizeof(uint);

    // The partitioner divides a buffer of a power-of-two size. Its backing partition is the full buffer, from offset 0.
    private static BinaryTreeBufferPartitioner CreatePartitioner(SharedBuffer buffer, VkBufferUsageFlags usage)
    {
        var allocation = new BufferAllocationInfo(buffer.Buffer, ByteSize.Zero, (ByteSize)SharedBufferBytes, usage, default);
        var backing = new BufferPartitionInfo(null!, allocation, ByteSize.Zero, (ByteSize)SharedBufferBytes);
        return new BinaryTreeBufferPartitioner(backing, maxElementSize: (ByteSize)SharedBufferBytes, minElementSize: (ByteSize)MinBlockBytes);
    }

    private void FreeReplacedChunks()
    {
        foreach (GpuChunk chunk in _replaced)
        {
            _vertexBlocks.Free(chunk.VertexBlock);
            _indexBlocks.Free(chunk.IndexBlock);
            _freeSlots.Push(chunk.Slot);
            BufferBytes -= chunk.Bytes;
            BlockBytes -= chunk.BlockBytes;
        }

        _replaced.Clear();
    }

    // Writes the vertices and the indices of the chunk to the staging buffer at the offset, gives the chunk two blocks and a slot,
    // and records the copy commands. Returns the offset after the data of the chunk.
    private long AddChunk(BuiltChunk chunk, VkBuffer staging, byte* mapped, long offset)
    {
        ReadOnlySpan<byte> vertices = MemoryMarshal.AsBytes(chunk.Mesh.Vertices.AsSpan());
        ReadOnlySpan<byte> indices = MemoryMarshal.AsBytes(chunk.Mesh.Indices.AsSpan());
        vertices.CopyTo(new Span<byte>(mapped + offset, vertices.Length));
        indices.CopyTo(new Span<byte>(mapped + offset + vertices.Length, indices.Length));

        int vertexBlock = _vertexBlocks.Alloc((ByteSize)vertices.Length);
        int indexBlock = vertexBlock < 0 ? -1 : _indexBlocks.Alloc((ByteSize)indices.Length);
        if (indexBlock < 0 || _freeSlots.Count == 0)
        {
            if (vertexBlock >= 0)
            {
                _vertexBlocks.Free(vertexBlock);
            }

            if (indexBlock >= 0)
            {
                _indexBlocks.Free(indexBlock);
            }

            throw new InvalidOperationException($"The shared chunk buffers have no space for the chunk at ({chunk.Position.X}, {chunk.Position.Y}, {chunk.Position.Z}): {vertices.Length} vertex bytes and {indices.Length} index bytes. Capacity: {SharedBufferBytes} bytes for each buffer and {MaxChunks} chunks.");
        }

        BinaryTreeBufferPartitioner.Range vertexRange = _vertexBlocks.GetRange(vertexBlock);
        BinaryTreeBufferPartitioner.Range indexRange = _indexBlocks.GetRange(indexBlock);
        long vertexOffset = (long)vertexRange.Offset;
        long indexOffset = (long)indexRange.Offset;
        int slot = _freeSlots.Pop();
        ((float4*)_positions.Address)[slot] = new float4(chunk.Position.X, chunk.Position.Y, chunk.Position.Z, 0f);

        var origin = new float3(chunk.Position.X, chunk.Position.Y, chunk.Position.Z);
        long blockBytes = (long)vertexRange.Size + (long)indexRange.Size;
        _chunks.Add(chunk.Position, new GpuChunk(
            vertexBlock,
            indexBlock,
            (int)(vertexOffset / ChunkVertex.SizeInBytes),
            (int)(indexOffset / sizeof(uint)),
            chunk.Mesh.Indices.Length,
            slot,
            vertices.Length + indices.Length,
            blockBytes,
            origin + chunk.Mesh.BoundsMin,
            origin + chunk.Mesh.BoundsMax));
        BufferBytes += vertices.Length + indices.Length;
        BlockBytes += blockBytes;

        _commandBuffer.CopyBuffer(staging, _vertices.Buffer, [new VkBufferCopy { SrcOffset = (ByteSize64)offset, DstOffset = (ByteSize64)vertexOffset, Size = (ByteSize64)vertices.Length }]);
        _commandBuffer.CopyBuffer(staging, _indices.Buffer, [new VkBufferCopy { SrcOffset = (ByteSize64)(offset + vertices.Length), DstOffset = (ByteSize64)indexOffset, Size = (ByteSize64)indices.Length }]);
        return offset + vertices.Length + indices.Length;
    }

    private (VkBuffer Buffer, VmaAllocation Allocation, nint MappedAddress) CreateStagingBuffer(long size)
    {
        MappedBuffer buffer = CreateMappedBuffer(size, VkBufferUsageFlags.TransferSrcBit);
        return (buffer.Buffer, buffer.Allocation, buffer.Address);
    }

    // A buffer that the CPU writes in order, with a permanent mapping. VMA selects a memory type that the CPU can write.
    private MappedBuffer CreateMappedBuffer(long size, VkBufferUsageFlags usage)
    {
        var bufferInfo = new VkBufferCreateInfo
        {
            Size = (ByteSize64)size,
            Usage = usage,
            SharingMode = VkSharingMode.Exclusive,
        };
        var allocationInfo = new VmaAllocationCreateInfo
        {
            Usage = VmaMemoryUsage.Auto,
            Flags = VmaAllocationCreateFlags.HostAccessSequentialWriteBit | VmaAllocationCreateFlags.MappedBit,
        };
        VkResult result = _gpu.Allocator.CreateBuffer(bufferInfo, allocationInfo, out VkBuffer buffer, out VmaAllocation allocation, out VmaAllocationInfo info);
        if (result != VkResult.Success)
        {
            throw new InvalidOperationException($"VMA cannot create a mapped buffer of {size} bytes: {result}.");
        }

        return new MappedBuffer(buffer, allocation, (nint)(void*)info.PMappedData);
    }

    private SharedBuffer CreateDeviceBuffer(long size, VkBufferUsageFlags usage)
    {
        var bufferInfo = new VkBufferCreateInfo
        {
            Size = (ByteSize64)size,
            Usage = usage | VkBufferUsageFlags.TransferDstBit,
            SharingMode = VkSharingMode.Exclusive,
        };

        // With no host access flag, VMA selects device-local memory.
        var allocationInfo = new VmaAllocationCreateInfo { Usage = VmaMemoryUsage.Auto };
        VkResult result = _gpu.Allocator.CreateBuffer(bufferInfo, allocationInfo, out VkBuffer buffer, out VmaAllocation allocation, out _);
        if (result != VkResult.Success)
        {
            throw new InvalidOperationException($"VMA cannot create a chunk buffer of {size} bytes: {result}.");
        }

        return new SharedBuffer(buffer, allocation);
    }

    private readonly record struct SharedBuffer(VkBuffer Buffer, VmaAllocation Allocation)
    {
        public void Destroy(GpuContext gpu)
        {
            if (!Buffer.IsNull())
            {
                gpu.Allocator.DestroyBuffer(Buffer, Allocation);
            }
        }
    }

    private readonly record struct MappedBuffer(VkBuffer Buffer, VmaAllocation Allocation, nint Address)
    {
        public void Destroy(GpuContext gpu)
        {
            if (!Buffer.IsNull())
            {
                gpu.Allocator.DestroyBuffer(Buffer, Allocation);
            }
        }
    }

    private readonly record struct GpuChunk(
        int VertexBlock,
        int IndexBlock,
        int VertexOffset,
        int FirstIndex,
        int IndexCount,
        int Slot,
        long Bytes,
        long BlockBytes,
        float3 BoundsMin,
        float3 BoundsMax);
}
