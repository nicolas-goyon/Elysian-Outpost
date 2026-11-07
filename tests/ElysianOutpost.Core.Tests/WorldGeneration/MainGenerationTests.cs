// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Core.Tests.WorldGeneration;

public sealed class MainGenerationTests
{
    private static readonly WorldSettings _scene = WorldSettings.SampleScene;

    [Fact]
    public void GenerateChunkAt_SameSeed_GivesTheSameChunk()
    {
        var first = new MainGeneration(_scene.ChunkSize, _scene.Seed);
        var second = new MainGeneration(_scene.ChunkSize, _scene.Seed);

        ushort[,,] firstChunk = first.GenerateChunkAt(new int3(0, 0, 0));
        ushort[,,] secondChunk = second.GenerateChunkAt(new int3(0, 0, 0));

        Assert.Equal(firstChunk.Cast<ushort>(), secondChunk.Cast<ushort>());
    }

    [Fact]
    public void GenerateChunkAt_DifferentSeed_GivesADifferentChunk()
    {
        var first = new MainGeneration(_scene.ChunkSize, 123);
        var second = new MainGeneration(_scene.ChunkSize, 124);

        ushort[,,] firstChunk = first.GenerateChunkAt(new int3(0, 0, 0));
        ushort[,,] secondChunk = second.GenerateChunkAt(new int3(0, 0, 0));

        Assert.NotEqual(firstChunk.Cast<ushort>(), secondChunk.Cast<ushort>());
    }

    [Fact]
    public void GenerateChunkAt_SceneSettings_GivesAChunkWithTheChunkSize()
    {
        var generation = new MainGeneration(_scene.ChunkSize, _scene.Seed);

        ushort[,,] chunk = generation.GenerateChunkAt(new int3(-16, 0, 32));

        Assert.Equal(16, chunk.GetLength(0));
        Assert.Equal(64, chunk.GetLength(1));
        Assert.Equal(16, chunk.GetLength(2));
    }

    // The reference values come from the legacy code (TerrainGeneration/MainGeneration.cs) for the seed 123 and the
    // chunk size of the scene. The legacy code ran outside Unity, with the Mathf methods of the Unity reference source.
    // Block types: 0 air, 8 water, 11 dirt, 13 sand, 16 grass, 22 stone, 24 bedrock.
    [Theory]
    [InlineData(0, 0, 0, 24)]
    [InlineData(0, 1, 0, 22)]
    [InlineData(0, 33, 0, 11)]
    [InlineData(0, 36, 0, 16)]
    [InlineData(0, 37, 0, 0)]
    [InlineData(15, 9, 86, 13)]
    [InlineData(15, 10, 86, 8)]

    // Negative positions. A wrong rounding mode or Truncate instead of Floor changes these values.
    [InlineData(-200, 36, -5, 16)]
    [InlineData(-39, 36, -39, 16)]

    // The float arithmetic decides this value. With double-precision arithmetic, the voxel is air (0).
    [InlineData(-85, 36, 120, 16)]
    public void GenerateChunkAt_Seed123_GivesTheReferenceBlockTypes(int x, int y, int z, ushort expected)
    {
        var generation = new MainGeneration(_scene.ChunkSize, _scene.Seed);
        int3 size = _scene.ChunkSize;
        var chunkPosition = new int3(
            (int)MathF.Floor((float)x / size.X) * size.X,
            (int)MathF.Floor((float)y / size.Y) * size.Y,
            (int)MathF.Floor((float)z / size.Z) * size.Z);

        ushort[,,] chunk = generation.GenerateChunkAt(chunkPosition);

        Assert.Equal(expected, chunk[x - chunkPosition.X, y - chunkPosition.Y, z - chunkPosition.Z]);
    }

    // The acceptance test of #49: with 2 chunk layers of 32 voxels, the terrain uses the full world height of 64 voxels.
    // These settings give the same terrain as one layer of 64 voxels, cut into two layers.
    [Fact]
    public void GenerateChunkAt_TwoChunkLayers_GivesTheTerrainOfTheWorldHeight()
    {
        var oneLayer = new MainGeneration(new int3(16, 64, 16), 123, chunkLayers: 1);
        var twoLayers = new MainGeneration(new int3(16, 32, 16), 123, chunkLayers: 2);

        ushort[,,] full = oneLayer.GenerateChunkAt(new int3(0, 0, 0));
        ushort[,,] lower = twoLayers.GenerateChunkAt(new int3(0, 0, 0));
        ushort[,,] upper = twoLayers.GenerateChunkAt(new int3(0, 32, 0));

        int solidInUpper = 0;
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    ushort split = y < 32 ? lower[x, y, z] : upper[x, y - 32, z];
                    Assert.Equal(full[x, y, z], split);
                    solidInUpper += y >= 32 && split != 0 ? 1 : 0;
                }
            }
        }

        // The terrain at (0, 0) has its grass at y = 36: in the second layer.
        Assert.True(solidInUpper > 0);
        Assert.Equal(16, upper[0, 36 - 32, 0]);
    }

    // The legacy code returned the block type ERROR (26) at y = 159 and y = 160 under a high terrain. #49 removes this debug code.
    [Fact]
    public void GenerateChunkAt_HighWorld_HasNoErrorBlock()
    {
        var generation = new MainGeneration(new int3(16, 64, 16), 123, chunkLayers: 4);
        for (int layer = 0; layer < 4; layer++)
        {
            foreach (int3 column in new[] { new int3(0, 0, 0), new int3(-200, 0, -5), new int3(160, 0, 96) })
            {
                ushort[,,] chunk = generation.GenerateChunkAt(new int3(column.X, layer * 64, column.Z));
                Assert.DoesNotContain((ushort)26, chunk.Cast<ushort>());
            }
        }
    }

    [Fact]
    public void Constructor_WithLessThanOneChunkLayer_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MainGeneration(new int3(16, 64, 16), 123, chunkLayers: 0));
    }
}
