// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.App.Rendering;

/// <summary>
/// The GPU memory of the app (#7): all the allocations of the Vulkan Memory Allocator (VMA), and the buffers of the chunk meshes.
/// </summary>
/// <param name="AllocationCount">The number of VMA allocations: the buffers and the images of the app.</param>
/// <param name="AllocationBytes">The bytes of the VMA allocations.</param>
/// <param name="BlockBytes">
/// The bytes of the memory blocks that VMA got from the driver. VMA puts many allocations in one block.
/// Thus this value is usually more than <paramref name="AllocationBytes"/>.
/// </param>
/// <param name="ChunkBufferCount">The number of buffers of the chunk meshes: two buffers for each chunk that has quads.</param>
/// <param name="ChunkBufferBytes">The bytes of the mesh data of the chunks.</param>
/// <param name="ChunkBlockBytes">The bytes of the blocks of the shared chunk buffers that the meshes use (#52). A block has a size of a power of two.</param>
/// <param name="ChunkCapacityBytes">The capacity of the shared vertex buffer and index buffer together (#52).</param>
internal readonly record struct GpuMemoryStatistics(long AllocationCount, long AllocationBytes, long BlockBytes, int ChunkBufferCount, long ChunkBufferBytes, long ChunkBlockBytes, long ChunkCapacityBytes);
