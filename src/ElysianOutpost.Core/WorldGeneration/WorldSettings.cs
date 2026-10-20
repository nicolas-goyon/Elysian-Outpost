// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.WorldGeneration;

/// <summary>
/// The settings of a world: the seed of the terrain, the size of the chunks and the size of the world.
/// </summary>
public sealed class WorldSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldSettings"/> class.
    /// </summary>
    /// <param name="seed">The seed of the terrain generation.</param>
    /// <param name="chunkSize">The number of voxels of a chunk on each axis.</param>
    /// <param name="radiusInChunks">The number of chunks from the center of the world to its border, on the X axis and on the Z axis.</param>
    /// <param name="chunkLayers">The number of chunks on the Y axis.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is zero or less, or the radius or the number of layers is negative.</exception>
    public WorldSettings(int seed, int3 chunkSize, int radiusInChunks, int chunkLayers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize.X, nameof(chunkSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize.Y, nameof(chunkSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize.Z, nameof(chunkSize));
        ArgumentOutOfRangeException.ThrowIfNegative(radiusInChunks);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkLayers);

        Seed = seed;
        ChunkSize = chunkSize;
        RadiusInChunks = radiusInChunks;
        ChunkLayers = chunkLayers;
    }

    /// <summary>
    /// Gets the settings of the scene <c>SampleScene</c> of the legacy project.
    /// These settings are the seed 123, chunks of 16 × 64 × 16 voxels, a radius of 16 chunks and one chunk layer.
    /// In this scene, the center of the world is at the origin.
    /// </summary>
    public static WorldSettings SampleScene { get; } = new(seed: 123, chunkSize: new int3(16, 64, 16), radiusInChunks: 16, chunkLayers: 1);

    /// <summary>
    /// Gets the seed of the terrain generation.
    /// </summary>
    public int Seed { get; }

    /// <summary>
    /// Gets the number of voxels of a chunk on each axis.
    /// </summary>
    public int3 ChunkSize { get; }

    /// <summary>
    /// Gets the number of chunks from the center of the world to its border, on the X axis and on the Z axis.
    /// </summary>
    public int RadiusInChunks { get; }

    /// <summary>
    /// Gets the number of chunks on the Y axis.
    /// </summary>
    public int ChunkLayers { get; }

    /// <summary>
    /// Gives the positions of all the chunks of the world, in voxel coordinates.
    /// </summary>
    /// <remarks>
    /// This method uses the logic of <c>FixedSizeTerrain.GetChunksInViewDistance</c> of the legacy project.
    /// The world is a square of 2 × <see cref="RadiusInChunks"/> chunks on each side, around the chunk that contains the center.
    /// The first chunk layer starts at y = 0. The method does not use the Y coordinate of the center.
    /// The order of the positions is: X, then Z, then Y.
    /// </remarks>
    /// <param name="center">The center of the world, in voxel coordinates.</param>
    /// <returns>The position of the first voxel of each chunk.</returns>
    public int3[] GetChunkPositions(int3 center)
    {
        int3[] chunks = new int3[(2 * RadiusInChunks) * (2 * RadiusInChunks) * ChunkLayers];
        int centerChunkX = (int)MathF.Floor((float)center.X / ChunkSize.X);
        int centerChunkZ = (int)MathF.Floor((float)center.Z / ChunkSize.Z);
        int xStart = (centerChunkX - RadiusInChunks) * ChunkSize.X;
        int zStart = (centerChunkZ - RadiusInChunks) * ChunkSize.Z;
        int xEnd = (centerChunkX + RadiusInChunks) * ChunkSize.X;
        int zEnd = (centerChunkZ + RadiusInChunks) * ChunkSize.Z;

        int index = 0;
        for (int x = xStart; x < xEnd; x += ChunkSize.X)
        {
            for (int z = zStart; z < zEnd; z += ChunkSize.Z)
            {
                for (int y = 0; y < ChunkLayers * ChunkSize.Y; y += ChunkSize.Y)
                {
                    chunks[index] = new int3(x, y, z);
                    index++;
                }
            }
        }

        return chunks;
    }
}
