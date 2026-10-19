// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Core.Tests.WorldGeneration;

public sealed class WorldSettingsTests
{
    [Fact]
    public void SampleScene_HasTheValuesOfTheLegacyScene()
    {
        WorldSettings scene = WorldSettings.SampleScene;

        Assert.Equal(123, scene.Seed);
        Assert.Equal(new int3(16, 64, 16), scene.ChunkSize);
        Assert.Equal(16, scene.RadiusInChunks);
        Assert.Equal(1, scene.ChunkLayers);
    }

    [Fact]
    public void GetChunkPositions_SampleSceneAtTheOrigin_GivesASquareOf32By32Chunks()
    {
        int3[] positions = WorldSettings.SampleScene.GetChunkPositions(new int3(0, 0, 0));

        Assert.Equal(32 * 32, positions.Length);
        Assert.Equal(positions.Length, positions.Distinct().Count());
        Assert.Equal(new int3(-256, 0, -256), positions[0]);
        Assert.Equal(new int3(-256, 0, -240), positions[1]);
        Assert.Equal(new int3(240, 0, 240), positions[^1]);
        Assert.All(positions, position =>
        {
            Assert.Equal(0, position.Y);
            Assert.InRange(position.X, -256, 240);
            Assert.InRange(position.Z, -256, 240);
            Assert.Equal(0, position.X % 16);
            Assert.Equal(0, position.Z % 16);
        });
    }

    [Fact]
    public void GetChunkPositions_TwoLayers_GivesTheLayersAfterEachOther()
    {
        var settings = new WorldSettings(seed: 1, chunkSize: new int3(16, 64, 16), radiusInChunks: 1, chunkLayers: 2);

        int3[] positions = settings.GetChunkPositions(new int3(0, 0, 0));

        int3[] expected =
        [
            new int3(-16, 0, -16), new int3(-16, 64, -16),
            new int3(-16, 0, 0), new int3(-16, 64, 0),
            new int3(0, 0, -16), new int3(0, 64, -16),
            new int3(0, 0, 0), new int3(0, 64, 0),
        ];
        Assert.Equal(expected, positions);
    }

    // The center (17, 5, -1) is in the chunk with the index 1 on the X axis and the index -1 on the Z axis.
    [Fact]
    public void GetChunkPositions_CenterInAnotherChunk_MovesTheSquare()
    {
        var settings = new WorldSettings(seed: 1, chunkSize: new int3(16, 64, 16), radiusInChunks: 2, chunkLayers: 1);

        int3[] positions = settings.GetChunkPositions(new int3(17, 5, -1));

        Assert.Equal(16, positions.Length);
        Assert.Equal(new int3(-16, 0, -48), positions[0]);
        Assert.Equal(new int3(32, 0, 0), positions[^1]);
    }

    [Fact]
    public void GetChunkPositions_RadiusZero_GivesNoPosition()
    {
        var settings = new WorldSettings(seed: 1, chunkSize: new int3(16, 64, 16), radiusInChunks: 0, chunkLayers: 1);

        Assert.Empty(settings.GetChunkPositions(new int3(0, 0, 0)));
    }

    [Theory]
    [InlineData(0, 64, 16, 1, 1)]
    [InlineData(16, -1, 16, 1, 1)]
    [InlineData(16, 64, 0, 1, 1)]
    [InlineData(16, 64, 16, -1, 1)]
    [InlineData(16, 64, 16, 1, -1)]
    public void Constructor_InvalidValue_ThrowsArgumentOutOfRangeException(int sizeX, int sizeY, int sizeZ, int radius, int layers)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorldSettings(seed: 1, chunkSize: new int3(sizeX, sizeY, sizeZ), radiusInChunks: radius, chunkLayers: layers));
    }
}
