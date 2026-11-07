// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.WorldGeneration;

/// <summary>
/// The terrain generation of the legacy project: a heightmap of Perlin noise with terrace steps, water up to the sea level, and bedrock at y = 0.
/// </summary>
/// <remarks>
/// The terrain height, the number of terrace steps and the sea level come from the world height (#49): the number of chunk layers × the chunk height.
/// Before #49, they came from the chunk height. Thus, with more than one chunk layer, the upper layers stayed empty.
/// With one chunk layer, the two heights are equal, and the terrain does not change.
/// </remarks>
public class MainGeneration
{
    private readonly int3 _chunkSize;
    private readonly int _worldHeight;
    private readonly FrequencyLayer[] _frequencyLayers;
    private const uint StepHeight = 8;
    private readonly int _seaLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainGeneration"/> class.
    /// </summary>
    /// <param name="chunkSize">The number of voxels of a chunk on each axis.</param>
    /// <param name="seed">The seed of the noise.</param>
    /// <param name="chunkLayers">The number of chunk layers of the world. The world height is this number × the chunk height.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkLayers"/> is less than 1.</exception>
    public MainGeneration(int3 chunkSize, int seed = 0, int chunkLayers = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkLayers, 1);
        this._chunkSize = chunkSize;
        _worldHeight = chunkLayers * chunkSize.Y;
        float amountOfSteps = ((float)_worldHeight) / StepHeight;
        _seaLevel = (int)MathF.Round(amountOfSteps * 2) - 1;
        PerlinNoise noise1 = new(seed);
        _frequencyLayers = new FrequencyLayer[]{
            new (
                frequency:.005f,
                amplitude:2.0f,
                noise:noise1,
                terraceSteps:amountOfSteps,
                roundPositionDigits:10
            ),
        };
    }

    /// <summary>
    /// Generates the voxels of a chunk.
    /// </summary>
    /// <param name="chunkPosition">The world position of the first voxel of the chunk. Each coordinate is a multiple of the chunk size.</param>
    /// <returns>The block types of the chunk, in an array of the chunk size.</returns>
    public ushort[,,] GenerateChunkAt(int3 chunkPosition)
    {
        int chunkX = chunkPosition.X;
        int chunkY = chunkPosition.Y;
        int chunkZ = chunkPosition.Z;

        ushort[,,] chunk = new ushort[_chunkSize.X, _chunkSize.Y, _chunkSize.Z];

        for (int x = 0; x < _chunkSize.X; x++)
        {
            for (int z = 0; z < _chunkSize.Z; z++)
            {
                for (int y = 0; y < _chunkSize.Y; y++)
                {
                    ushort? blockType = GetBlockTypeAt(chunkPosition, new int3(x, y, z));
                    if (blockType.HasValue)
                    {
                        chunk[x, y, z] = blockType.Value;
                    }
                }
            }
        }

        return chunk;
    }

    private enum BlockType : ushort
    {
        AIR = 0,
        GRASS = 16,
        DIRT = 11,
        STONE = 22,
        SAND = 13,
        WATER = 8,
        BEDROCK = 24,
    }


    private ushort? GetBlockTypeAt(int3 chunkPosition, int3 localPosition)
    {
        // The world position of the voxel.
        int worldX = chunkPosition.X + localPosition.X;
        int worldY = chunkPosition.Y + localPosition.Y;
        int worldZ = chunkPosition.Z + localPosition.Z;

        // 1. The noise gives a height from 0 to 1. The frequency of the noise gives plateaus of about 100 voxels.
        float height01 = GetHeight01At(worldX, worldZ);

        // 2. The terrain height, from 0 to the world height. The noise layer already makes the terrace steps.
        int terrainHeight = (int)MathF.Floor(height01 * _worldHeight);

        // 3. The block type from the height. All the voxels at y = 0 and below are bedrock.
        if (worldY <= 0)
            return (ushort)BlockType.BEDROCK;

        // Above the terrain: water up to the sea level, then air (the default value of the chunk).
        if (worldY > terrainHeight)
        {
            if (worldY <= _seaLevel)
                return (ushort)BlockType.WATER;

            return null;
        }

        // In the terrain: grass or sand at the surface, then three voxels of dirt, then stone.
        int depthFromSurface = terrainHeight - worldY;

        return depthFromSurface switch
        {
            // Sand near the water.
            0 when terrainHeight <= _seaLevel + 1 => (ushort)BlockType.SAND,
            0 => (ushort)BlockType.GRASS,
            <= 3 => (ushort)BlockType.DIRT,
            _ => (ushort)BlockType.STONE
        };
    }

    // The terrain height from 0 to 1: the weighted mean of the heights of all the layers.
    private float GetHeight01At(int worldX, int worldZ)
    {
        float sum = 0f;
        float totalAmp = 0f;

        foreach (FrequencyLayer layer in _frequencyLayers)
        {
            (float ampNoised, float amp) = layer.GetHeight01At(worldX, worldZ);

            sum += ampNoised;
            totalAmp += amp;
        }

        return totalAmp <= 0f ? 0f : Math.Clamp(sum / totalAmp, 0f, 1f);
    }
}
