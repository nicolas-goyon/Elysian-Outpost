// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// One vertex of a chunk mesh, in the memory layout of the vertex buffer of the chunk renderer.
/// </summary>
/// <remarks>
/// The layout is sequential, with no padding: the position at byte 0, the normal at byte 12 and the color at byte 24.
/// The renderer gives these offsets to the vertex shader. A test examines the layout.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct ChunkVertex
{
    /// <summary>
    /// The size of one vertex, in bytes.
    /// </summary>
    public const int SizeInBytes = 28;

    /// <summary>
    /// The position of the vertex in the chunk, in voxel units. The renderer adds the world position of the chunk.
    /// </summary>
    public float3 Position;

    /// <summary>
    /// The unit normal of the quad of the vertex, in world axes.
    /// </summary>
    public float3 Normal;

    /// <summary>
    /// The color of the block type, as the palette gives it: red, green, blue and alpha, with sRGB values.
    /// </summary>
    public byte4 Color;
}
