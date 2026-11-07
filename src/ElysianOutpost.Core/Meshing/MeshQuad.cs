// SPDX-License-Identifier: MPL-2.0

using System.Numerics;

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// One rectangular face of a mesh: four corners, a normal and a block type.
/// </summary>
/// <remarks>
/// The corners are in chunk coordinates, in voxel units. They go around the rectangle in order.
/// The renderer draws the quad as the two triangles (0, 1, 2) and (2, 3, 0), as the legacy project did.
/// </remarks>
public record MeshQuad
{
    /// <summary>
    /// The first corner.
    /// </summary>
    public Vector3 Vertex0;

    /// <summary>
    /// The second corner.
    /// </summary>
    public Vector3 Vertex1;

    /// <summary>
    /// The third corner: the corner opposite to <see cref="Vertex0"/>.
    /// </summary>
    public Vector3 Vertex2;

    /// <summary>
    /// The fourth corner.
    /// </summary>
    public Vector3 Vertex3;

    /// <summary>
    /// The unit normal of the face. It points from the solid voxel to the outside (#46).
    /// </summary>
    public Vector3 Normal;

    /// <summary>
    /// The block type of the face: an index in the <see cref="Voxels.BlockPalette"/>.
    /// </summary>
    public uint VoxelID;
}
