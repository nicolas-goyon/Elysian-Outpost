// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// The GPU buffers of the chunk meshes: one vertex buffer and one index buffer for each chunk.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Upload"/> copies a group of chunks to the GPU through one staging buffer, then waits until the GPU completes the copy.
/// The wait also includes the frames in flight: the signal of a fence comes after all the earlier submissions to the queue
/// (Vulkan specification, "Fence Signaling"). The renderer submits the frames to the same queue.
/// </para>
/// <para>
/// Each chunk has its own buffers. Thus an upload for a chunk that has buffers replaces only the buffers of that chunk (#41).
/// After the wait, the GPU does not use the old buffers, and the upload releases them.
/// </para>
/// </remarks>
internal sealed unsafe class ChunkMeshes : IDisposable
{
    private readonly GpuContext _gpu;
    private readonly Dictionary<int3, GpuChunk> _chunks = [];
    private readonly List<GpuChunk> _replaced = [];
    private readonly VkCommandPool _commandPool;
    private readonly CommandBuffer _commandBuffer;
    private readonly VkFence _fence;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkMeshes"/> class, with no chunk.
    /// </summary>
    /// <param name="gpu">The Vulkan objects of the window.</param>
    public ChunkMeshes(GpuContext gpu)
    {
        _gpu = gpu;
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
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the number of chunks with GPU buffers. A chunk with no quad has no buffers.
    /// </summary>
    public int ChunkCount => _chunks.Count;

    /// <summary>
    /// Gets the total time of the uploads, from the creation of the staging buffer to the end of the copy on the GPU.
    /// </summary>
    public TimeSpan UploadTime { get; private set; }

    /// <summary>
    /// Copies the meshes of a group of chunks to new GPU buffers, and waits until the GPU completes the copy.
    /// If a chunk has buffers, the new buffers replace them, and the method releases the old buffers after the wait.
    /// </summary>
    /// <param name="chunks">
    /// The chunks. Each position must occur only one time. A chunk with no quad gets no buffers: the method only releases its old buffers.
    /// </param>
    /// <exception cref="ArgumentException">A position occurs two times in <paramref name="chunks"/>.</exception>
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

        // With no quad to copy, the command buffer has only the barrier. Its fence still waits for the frames that use the old buffers.
        (VkBuffer staging, VmaAllocation stagingAllocation, nint mappedAddress) = stagingSize > 0
            ? CreateStagingBuffer(stagingSize)
            : (VkBuffer.NullHandle, default(VmaAllocation), 0);
        byte* mapped = (byte*)mappedAddress;
        try
        {
            _commandBuffer.Reset();
            _commandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);
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

            // The draws of the next frames read the new buffers. They must wait for the copy.
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

            // The GPU completed all the frames that used the old buffers. If an exception occurs before this line,
            // the GPU can still use them. Then Dispose releases them after WaitIdle.
            ReleaseReplacedBuffers();
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
    /// Records the draw commands of all the chunks. The pipeline must be bound, with the view-projection matrix in its push constants.
    /// </summary>
    /// <param name="commandBuffer">The command buffer, in a rendering scope.</param>
    /// <param name="layout">The layout of the chunk pipeline.</param>
    public void Record(CommandBuffer commandBuffer, VkPipelineLayout layout)
    {
        foreach (GpuChunk chunk in _chunks.Values)
        {
            float4 position = chunk.Position;
            commandBuffer.BindVertexBuffers(0, [chunk.VertexBuffer], [default(ByteSize64)]);
            commandBuffer.BindIndexBuffer(chunk.IndexBuffer, default, VkIndexType.UInt32);
            commandBuffer.PushConstants(layout, VkShaderStageFlags.VertexBit, ChunkPipeline.ChunkPositionOffset, MemoryMarshal.AsBytes(new ReadOnlySpan<float4>(ref position)));
            commandBuffer.DrawIndexed(chunk.IndexCount, 1, 0, 0, 0);
        }
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
        foreach (GpuChunk chunk in _chunks.Values)
        {
            Release(chunk);
        }

        _chunks.Clear();
        ReleaseReplacedBuffers();
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

    private void ReleaseReplacedBuffers()
    {
        foreach (GpuChunk chunk in _replaced)
        {
            Release(chunk);
        }

        _replaced.Clear();
    }

    private void Release(GpuChunk chunk)
    {
        _gpu.Allocator.DestroyBuffer(chunk.VertexBuffer, chunk.VertexAllocation);
        _gpu.Allocator.DestroyBuffer(chunk.IndexBuffer, chunk.IndexAllocation);
    }

    private static long VertexBytes(ChunkMeshData mesh) => (long)mesh.Vertices.Length * ChunkVertex.SizeInBytes;

    private static long IndexBytes(ChunkMeshData mesh) => (long)mesh.Indices.Length * sizeof(uint);

    // Writes the vertices and the indices of the chunk to the staging buffer at the offset.
    // Then creates the GPU buffers of the chunk and records the copy commands. Returns the offset after the data of the chunk.
    private long AddChunk(BuiltChunk chunk, VkBuffer staging, byte* mapped, long offset)
    {
        ReadOnlySpan<byte> vertices = MemoryMarshal.AsBytes(chunk.Mesh.Vertices.AsSpan());
        ReadOnlySpan<byte> indices = MemoryMarshal.AsBytes(chunk.Mesh.Indices.AsSpan());
        vertices.CopyTo(new Span<byte>(mapped + offset, vertices.Length));
        indices.CopyTo(new Span<byte>(mapped + offset + vertices.Length, indices.Length));

        (VkBuffer vertexBuffer, VmaAllocation vertexAllocation) = CreateDeviceBuffer(vertices.Length, VkBufferUsageFlags.VertexBufferBit);
        (VkBuffer indexBuffer, VmaAllocation indexAllocation) = (VkBuffer.NullHandle, default(VmaAllocation));
        try
        {
            (indexBuffer, indexAllocation) = CreateDeviceBuffer(indices.Length, VkBufferUsageFlags.IndexBufferBit);
        }
        catch
        {
            _gpu.Allocator.DestroyBuffer(vertexBuffer, vertexAllocation);
            throw;
        }

        _chunks.Add(chunk.Position, new GpuChunk(
            vertexBuffer,
            vertexAllocation,
            indexBuffer,
            indexAllocation,
            chunk.Mesh.Indices.Length,
            new float4(chunk.Position.X, chunk.Position.Y, chunk.Position.Z, 0f)));

        _commandBuffer.CopyBuffer(staging, vertexBuffer, [new VkBufferCopy { SrcOffset = (ByteSize64)offset, DstOffset = default, Size = (ByteSize64)vertices.Length }]);
        _commandBuffer.CopyBuffer(staging, indexBuffer, [new VkBufferCopy { SrcOffset = (ByteSize64)(offset + vertices.Length), DstOffset = default, Size = (ByteSize64)indices.Length }]);
        return offset + vertices.Length + indices.Length;
    }

    private (VkBuffer Buffer, VmaAllocation Allocation, nint MappedAddress) CreateStagingBuffer(long size)
    {
        var bufferInfo = new VkBufferCreateInfo
        {
            Size = (ByteSize64)size,
            Usage = VkBufferUsageFlags.TransferSrcBit,
            SharingMode = VkSharingMode.Exclusive,
        };

        // The CPU writes the staging buffer one time, in order. VMA selects a memory type that the CPU can write.
        var allocationInfo = new VmaAllocationCreateInfo
        {
            Usage = VmaMemoryUsage.Auto,
            Flags = VmaAllocationCreateFlags.HostAccessSequentialWriteBit | VmaAllocationCreateFlags.MappedBit,
        };
        VkResult result = _gpu.Allocator.CreateBuffer(bufferInfo, allocationInfo, out VkBuffer buffer, out VmaAllocation allocation, out VmaAllocationInfo info);
        if (result != VkResult.Success)
        {
            throw new InvalidOperationException($"VMA cannot create a staging buffer of {size} bytes: {result}.");
        }

        return (buffer, allocation, (nint)(void*)info.PMappedData);
    }

    private (VkBuffer Buffer, VmaAllocation Allocation) CreateDeviceBuffer(long size, VkBufferUsageFlags usage)
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

        return (buffer, allocation);
    }

    private readonly record struct GpuChunk(
        VkBuffer VertexBuffer,
        VmaAllocation VertexAllocation,
        VkBuffer IndexBuffer,
        VmaAllocation IndexAllocation,
        int IndexCount,
        float4 Position);
}
