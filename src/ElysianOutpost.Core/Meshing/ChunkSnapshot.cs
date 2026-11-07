// SPDX-License-Identifier: MPL-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal.Numerics;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// An immutable copy of the data that the mesh of one chunk needs (#47): the voxels of the chunk,
/// and the border layer of each of its six neighbor chunks. A worker thread meshes the snapshot while the main thread changes the world.
/// </summary>
/// <remarks>
/// <para>
/// The snapshot copies about 41 KB for a chunk of 16 × 64 × 16 voxels: 32 KB for the voxels and 9 KB for the borders
/// (2 KB for each X and Z border, 0.5 KB for each Y border).
/// A neighbor chunk that the world does not have gives no border: its voxels are air.
/// </para>
/// <para>
/// The <see cref="Version"/> tells the caller if the result is still valid. The <see cref="WorldGeneration.ChunkStreamer"/> changes the version
/// of a chunk at each change of its data.
/// </para>
/// </remarks>
public sealed class ChunkSnapshot
{
    private readonly ushort[] _voxels;
    private readonly ushort[]?[] _borders;

    private ChunkSnapshot(int3 position, int3 size, int version, ushort[] voxels, ushort[]?[] borders)
    {
        Position = position;
        Size = size;
        Version = version;
        _voxels = voxels;
        _borders = borders;
    }

    /// <summary>
    /// Gets the world position of the first voxel of the chunk.
    /// </summary>
    public int3 Position { get; }

    /// <summary>
    /// Gets the number of voxels of the chunk on each axis.
    /// </summary>
    public int3 Size { get; }

    /// <summary>
    /// Gets the version of the chunk data when the snapshot was taken.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Gets the block types of the chunk, in the order of a <c>ushort[x, y, z]</c> array.
    /// </summary>
    public ReadOnlySpan<ushort> Voxels => _voxels;

    /// <summary>
    /// Takes a snapshot of a chunk of a world.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="position">The position of the chunk.</param>
    /// <param name="version">The version of the chunk data.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The world has no chunk at this position.</exception>
    public static ChunkSnapshot Take(VoxelWorld world, int3 position, int version)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.TryGetChunk(position, out ushort[,,]? voxels))
        {
            throw new ArgumentException($"The world has no chunk at ({position.X}, {position.Y}, {position.Z}).", nameof(position));
        }

        ushort[] copy = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetArrayDataReference(voxels)), voxels.Length).ToArray();
        int3 size = world.ChunkSize;
        var borders = new ushort[]?[6];
        for (int direction = 0; direction < borders.Length; direction++)
        {
            if (world.TryGetChunk(position + GreedyMesher.NeighborOffset(size, direction), out ushort[,,]? neighbor))
            {
                var border = new ushort[GreedyMesher.BorderLength(size, direction)];
                GreedyMesher.CopyBorder(neighbor, direction, border);
                borders[direction] = border;
            }
        }

        return new ChunkSnapshot(position, size, version, copy, borders);
    }

    /// <summary>
    /// Gets the border layer of the neighbor chunk in a direction, or an empty span for a neighbor of air.
    /// </summary>
    /// <param name="direction">The index of the direction, from 0 to 5, in the order −X, +X, −Y, +Y, −Z, +Z.</param>
    /// <returns>The border layer.</returns>
    internal ReadOnlySpan<ushort> GetBorder(int direction) => _borders[direction];
}
