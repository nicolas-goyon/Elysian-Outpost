// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Benchmarks;

/// <summary>
/// The chunks of the benchmarks. All the chunks have the size of the sample scene: 16 × 64 × 16 voxels.
/// </summary>
internal static class ChunkData
{
    /// <summary>
    /// Gets the world of the benchmarks: the sample scene of the app (seed 123, 1024 chunks).
    /// </summary>
    public static WorldSettings World => WorldSettings.SampleScene;

    /// <summary>
    /// Gets the position of the terrain chunk of the benchmarks. This chunk is at the center of the sample scene.
    /// </summary>
    public static int3 TerrainChunkPosition => default;

    /// <summary>
    /// Makes a chunk with a checkerboard of solid voxels and air in the three directions.
    /// Each solid voxel has six visible faces, and no face can merge with a neighbor face. Thus this chunk is the worst case of the mesher.
    /// </summary>
    /// <returns>The block types of the chunk, with the indices [x, y, z].</returns>
    public static ushort[,,] MakeCheckerboard()
    {
        int3 size = World.ChunkSize;
        ushort[,,] voxels = new ushort[size.X, size.Y, size.Z];
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                for (int z = 0; z < size.Z; z++)
                {
                    voxels[x, y, z] = (x + y + z) % 2 == 0 ? (ushort)22 : (ushort)0;
                }
            }
        }

        return voxels;
    }
}
