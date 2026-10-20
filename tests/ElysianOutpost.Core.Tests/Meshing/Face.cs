// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// One face of one voxel.
/// </summary>
/// <param name="X">The X position of the voxel in the chunk.</param>
/// <param name="Y">The Y position of the voxel in the chunk.</param>
/// <param name="Z">The Z position of the voxel in the chunk.</param>
/// <param name="Direction">The direction of the face, from the voxel to its neighbor.</param>
internal readonly record struct Face(int X, int Y, int Z, FaceDirection Direction)
{
    /// <summary>
    /// Gets the position of the neighbor voxel, on the other side of the face. This position can be outside the chunk.
    /// </summary>
    public (int X, int Y, int Z) Neighbor
    {
        get
        {
            int step = Direction.IsPositive() ? 1 : -1;
            return Direction.Axis() switch
            {
                0 => (X + step, Y, Z),
                1 => (X, Y + step, Z),
                _ => (X, Y, Z + step),
            };
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Direction.Label()} face of the voxel ({X}, {Y}, {Z})";
}
