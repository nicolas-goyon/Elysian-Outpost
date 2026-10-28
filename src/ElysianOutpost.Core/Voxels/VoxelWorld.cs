// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.Voxels;

/// <summary>
/// The block types of the voxels of the world, chunk by chunk, in world coordinates.
/// </summary>
/// <remarks>
/// <para>
/// All the chunks have the same size. The position of a chunk is the world position of its first voxel.
/// Each coordinate of this position is a multiple of the chunk size on the same axis.
/// </para>
/// <para>
/// A position outside all the chunks is empty (block type 0).
/// The world limits are the box around all the chunks (<see cref="Min"/> and <see cref="Max"/>).
/// </para>
/// <para>
/// A chunk uses 2 bytes for each voxel. The chunks of the sample scene use 32 MiB.
/// </para>
/// </remarks>
public sealed class VoxelWorld
{
    private readonly Dictionary<int3, ushort[,,]> _chunks = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="VoxelWorld"/> class, with no chunk.
    /// </summary>
    /// <param name="chunkSize">The number of voxels of a chunk on each axis.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is zero or less.</exception>
    public VoxelWorld(int3 chunkSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize.X, nameof(chunkSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize.Y, nameof(chunkSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize.Z, nameof(chunkSize));
        ChunkSize = chunkSize;
    }

    /// <summary>
    /// Gets the number of voxels of a chunk on each axis.
    /// </summary>
    public int3 ChunkSize { get; }

    /// <summary>
    /// Gets the number of chunks.
    /// </summary>
    public int ChunkCount => _chunks.Count;

    /// <summary>
    /// Gets the lowest corner of the box around all the chunks: the minimum of the chunk positions.
    /// The value is (0, 0, 0) if the world has no chunk.
    /// </summary>
    public int3 Min { get; private set; }

    /// <summary>
    /// Gets the highest corner of the box around all the chunks. This corner is not in the box:
    /// the box contains a voxel position p if <see cref="Min"/> ≤ p &lt; <see cref="Max"/> on each axis.
    /// The value is (0, 0, 0) if the world has no chunk.
    /// </summary>
    public int3 Max { get; private set; }

    /// <summary>
    /// Adds a chunk. The world keeps the array and does not copy it. Thus a change of the array changes the world.
    /// </summary>
    /// <param name="position">The world position of the first voxel of the chunk. Each coordinate is a multiple of the chunk size.</param>
    /// <param name="voxels">The block type of each voxel, with the indices [x, y, z]. The lengths are the chunk size.</param>
    /// <exception cref="ArgumentNullException"><paramref name="voxels"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// The lengths of the array are not the chunk size, the position is not a multiple of the chunk size, or the world has a chunk at this position.
    /// </exception>
    public void AddChunk(int3 position, ushort[,,] voxels)
    {
        ArgumentNullException.ThrowIfNull(voxels);
        if (voxels.GetLength(0) != ChunkSize.X || voxels.GetLength(1) != ChunkSize.Y || voxels.GetLength(2) != ChunkSize.Z)
        {
            throw new ArgumentException(
                $"The array has {voxels.GetLength(0)} x {voxels.GetLength(1)} x {voxels.GetLength(2)} voxels. The chunk size is {ChunkSize.X} x {ChunkSize.Y} x {ChunkSize.Z}.",
                nameof(voxels));
        }

        if (position.X % ChunkSize.X != 0 || position.Y % ChunkSize.Y != 0 || position.Z % ChunkSize.Z != 0)
        {
            throw new ArgumentException($"The position ({position.X}, {position.Y}, {position.Z}) is not a multiple of the chunk size.", nameof(position));
        }

        if (!_chunks.TryAdd(position, voxels))
        {
            throw new ArgumentException($"The world already has a chunk at ({position.X}, {position.Y}, {position.Z}).", nameof(position));
        }

        int3 end = position + ChunkSize;
        if (_chunks.Count == 1)
        {
            Min = position;
            Max = end;
        }
        else
        {
            Min = new int3(Math.Min(Min.X, position.X), Math.Min(Min.Y, position.Y), Math.Min(Min.Z, position.Z));
            Max = new int3(Math.Max(Max.X, end.X), Math.Max(Max.Y, end.Y), Math.Max(Max.Z, end.Z));
        }
    }

    /// <summary>
    /// Gets the block type of the voxel at a world position.
    /// </summary>
    /// <param name="position">The world position of the voxel.</param>
    /// <returns>The block type, or 0 (empty) if no chunk contains the position.</returns>
    public ushort GetVoxel(int3 position)
    {
        int3 chunk = new(
            FloorToMultiple(position.X, ChunkSize.X),
            FloorToMultiple(position.Y, ChunkSize.Y),
            FloorToMultiple(position.Z, ChunkSize.Z));
        return _chunks.TryGetValue(chunk, out ushort[,,]? voxels)
            ? voxels[position.X - chunk.X, position.Y - chunk.Y, position.Z - chunk.Z]
            : (ushort)0;
    }

    /// <summary>
    /// Gets a value that tells if the voxel at a world position is solid: its block type is not 0.
    /// </summary>
    /// <param name="position">The world position of the voxel.</param>
    /// <returns><c>true</c> if the voxel is solid. <c>false</c> if it is empty or if no chunk contains the position.</returns>
    public bool IsSolid(int3 position) => GetVoxel(position) != 0;

    // The largest multiple of size that is not more than value. The division rounds toward zero, thus a negative value needs a correction.
    private static int FloorToMultiple(int value, int size)
    {
        int remainder = value % size;
        return remainder < 0 ? value - remainder - size : value - remainder;
    }
}
