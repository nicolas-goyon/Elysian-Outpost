// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// One vertex of a chunk mesh, in the memory layout of the vertex buffer of the chunk renderer: 8 bytes (#51).
/// </summary>
/// <remarks>
/// <para>
/// The first 32 bits contain the position in the chunk and the normal: X in bits 0 to 7, Y in bits 8 to 15, Z in bits 16 to 23,
/// and the index of the normal in bits 24 to 26. The next 32 bits contain the block type.
/// The vertex shader unpacks the values, and reads the color of the block type from the palette.
/// </para>
/// <para>
/// A coordinate goes from 0 to 255. Thus a chunk can have a maximum of 255 voxels on each axis. The sample scene has 16 × 64 × 16.
/// Before #51, a vertex had 28 bytes: float positions, float normals and the color.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct ChunkVertex
{
    /// <summary>
    /// The size of one vertex, in bytes.
    /// </summary>
    public const int SizeInBytes = 8;

    /// <summary>
    /// The maximum value of a coordinate of a vertex.
    /// </summary>
    public const int MaxCoordinate = 255;

    /// <summary>
    /// The position in the chunk (bits 0 to 23) and the index of the normal (bits 24 to 26). See the remarks of the class.
    /// </summary>
    public uint PositionAndNormal;

    /// <summary>
    /// The block type of the quad of the vertex. The shader reads its color from the palette.
    /// </summary>
    public uint BlockType;

    /// <summary>
    /// Gets the position of the vertex in the chunk, in voxel units.
    /// </summary>
    public readonly float3 Position => new(PositionAndNormal & 0xFF, (PositionAndNormal >> 8) & 0xFF, (PositionAndNormal >> 16) & 0xFF);

    /// <summary>
    /// Gets the index of the normal, from 0 to 5: −X, +X, −Y, +Y, −Z, +Z.
    /// </summary>
    public readonly int NormalIndex => (int)((PositionAndNormal >> 24) & 0x7);

    /// <summary>
    /// Gets the unit normal of the quad of the vertex.
    /// </summary>
    public readonly float3 Normal => Normals[NormalIndex];

    /// <summary>
    /// Gets the six normals, in the order of their index. The vertex shader has the same table.
    /// </summary>
    public static ReadOnlySpan<float3> Normals => _normals;

    private static readonly float3[] _normals =
    [
        new(-1, 0, 0), new(1, 0, 0),
        new(0, -1, 0), new(0, 1, 0),
        new(0, 0, -1), new(0, 0, 1),
    ];

    /// <summary>
    /// Makes a vertex.
    /// </summary>
    /// <param name="x">The X coordinate in the chunk, from 0 to <see cref="MaxCoordinate"/>.</param>
    /// <param name="y">The Y coordinate in the chunk, from 0 to <see cref="MaxCoordinate"/>.</param>
    /// <param name="z">The Z coordinate in the chunk, from 0 to <see cref="MaxCoordinate"/>.</param>
    /// <param name="normalIndex">The index of the normal, from 0 to 5.</param>
    /// <param name="blockType">The block type.</param>
    /// <returns>The vertex.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside its range.</exception>
    public static ChunkVertex Create(int x, int y, int z, int normalIndex, ushort blockType)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfNegative(z);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x, MaxCoordinate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(y, MaxCoordinate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(z, MaxCoordinate);
        ArgumentOutOfRangeException.ThrowIfNegative(normalIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(normalIndex, 5);
        return new ChunkVertex
        {
            PositionAndNormal = (uint)x | ((uint)y << 8) | ((uint)z << 16) | ((uint)normalIndex << 24),
            BlockType = blockType,
        };
    }
}
