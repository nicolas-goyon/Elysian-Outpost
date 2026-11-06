// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Tests.Meshing;

/// <summary>
/// Gives the positions of the voxels of a test chunk, with the indices [x, y, z].
/// </summary>
internal static class VoxelGrid
{
    /// <summary>
    /// Gives all the positions in the chunk.
    /// </summary>
    /// <param name="voxels">The block types of the chunk, with the indices [x, y, z].</param>
    /// <returns>The positions, in the order X, then Y, then Z.</returns>
    public static IEnumerable<(int X, int Y, int Z)> Positions(ushort[,,] voxels)
    {
        ArgumentNullException.ThrowIfNull(voxels);
        for (int x = 0; x < voxels.GetLength(0); x++)
        {
            for (int y = 0; y < voxels.GetLength(1); y++)
            {
                for (int z = 0; z < voxels.GetLength(2); z++)
                {
                    yield return (x, y, z);
                }
            }
        }
    }

    /// <summary>
    /// Gets a value that tells if a position is in the chunk.
    /// </summary>
    /// <param name="voxels">The block types of the chunk, with the indices [x, y, z].</param>
    /// <param name="position">The position.</param>
    /// <returns><c>true</c> if the position is in the chunk.</returns>
    public static bool IsInside(ushort[,,] voxels, (int X, int Y, int Z) position) =>
        position.X >= 0 && position.X < voxels.GetLength(0)
        && position.Y >= 0 && position.Y < voxels.GetLength(1)
        && position.Z >= 0 && position.Z < voxels.GetLength(2);
}
