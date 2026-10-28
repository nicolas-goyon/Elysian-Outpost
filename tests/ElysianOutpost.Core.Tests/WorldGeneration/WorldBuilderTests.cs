// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Core.Tests.WorldGeneration;

public sealed class WorldBuilderTests
{
    // Four chunks of the sample scene: a radius of 1 chunk around the origin.
    private static readonly WorldSettings _smallWorld = new(seed: 123, chunkSize: new int3(16, 64, 16), radiusInChunks: 1, chunkLayers: 1);

    [Fact]
    public void TryBuildNext_BuildsEachChunkInTheOrderOfTheChunkPositions()
    {
        var builder = new WorldBuilder(_smallWorld, BlockPalette.Game, default);
        int3[] expected = _smallWorld.GetChunkPositions(default);

        var positions = new List<int3>();
        while (builder.TryBuildNext(out BuiltChunk chunk))
        {
            positions.Add(chunk.Position);
        }

        Assert.Equal(expected, positions);
        Assert.Equal(4, builder.ChunkCount);
        Assert.Equal(4, builder.BuiltChunkCount);
        Assert.True(builder.IsComplete);
    }

    // The builder must give the same result as the terrain generation, the mesher and the mesh data, step by step.
    [Fact]
    public void TryBuildNext_GivesTheMeshDataOfTheGeneratedChunk()
    {
        var builder = new WorldBuilder(_smallWorld, BlockPalette.Game, default);
        var generation = new MainGeneration(_smallWorld.ChunkSize, _smallWorld.Seed);
        long quadCount = 0;

        while (builder.TryBuildNext(out BuiltChunk chunk))
        {
            var voxels = new Chunk(generation.GenerateChunkAt(chunk.Position), chunk.Position);
            ChunkMeshData expected = ChunkMeshData.Build(new DisjointSetMeshOptimizer(new Mesh()).Optimize(voxels).Quads, BlockPalette.Game);
            Assert.Equal(expected.Vertices, chunk.Mesh.Vertices);
            Assert.Equal(expected.Indices, chunk.Mesh.Indices);
            Assert.True(chunk.Mesh.QuadCount > 0);
            quadCount += chunk.Mesh.QuadCount;
        }

        Assert.Equal(quadCount, builder.QuadCount);
    }

    [Fact]
    public void TryBuildNext_AddsTheVoxelsOfEachChunkToTheVoxelWorld()
    {
        var builder = new WorldBuilder(_smallWorld, BlockPalette.Game, default);
        var generation = new MainGeneration(_smallWorld.ChunkSize, _smallWorld.Seed);
        Assert.Equal(_smallWorld.ChunkSize, builder.Voxels.ChunkSize);

        while (builder.TryBuildNext(out BuiltChunk chunk))
        {
            Assert.Equal(builder.BuiltChunkCount, builder.Voxels.ChunkCount);
            ushort[,,] expected = generation.GenerateChunkAt(chunk.Position);
            for (int x = 0; x < expected.GetLength(0); x++)
            {
                for (int y = 0; y < expected.GetLength(1); y++)
                {
                    for (int z = 0; z < expected.GetLength(2); z++)
                    {
                        Assert.Equal(expected[x, y, z], builder.Voxels.GetVoxel(chunk.Position + new int3(x, y, z)));
                    }
                }
            }
        }

        // A radius of 1 chunk around the origin: from -16 to 16 on the X and Z axes.
        Assert.Equal(new int3(-16, 0, -16), builder.Voxels.Min);
        Assert.Equal(new int3(16, 64, 16), builder.Voxels.Max);
    }

    [Fact]
    public void TryBuildNext_AfterTheLastChunk_ReturnsFalse()
    {
        var builder = new WorldBuilder(_smallWorld, BlockPalette.Game, default);
        while (builder.TryBuildNext(out _))
        {
        }

        Assert.False(builder.TryBuildNext(out BuiltChunk chunk));
        Assert.Equal(default, chunk);
        Assert.Equal(4, builder.BuiltChunkCount);
    }

    [Fact]
    public void NewBuilder_HasNoChunkAndNoTime()
    {
        var builder = new WorldBuilder(_smallWorld, BlockPalette.Game, default);

        Assert.Equal(0, builder.BuiltChunkCount);
        Assert.False(builder.IsComplete);
        Assert.Equal(0, builder.QuadCount);
        Assert.Equal(TimeSpan.Zero, builder.GenerationTime);
        Assert.Equal(TimeSpan.Zero, builder.MeshingTime);
    }

    [Fact]
    public void TryBuildNext_AddsTheTimeOfEachStep()
    {
        var builder = new WorldBuilder(_smallWorld, BlockPalette.Game, default);

        builder.TryBuildNext(out _);

        Assert.True(builder.GenerationTime > TimeSpan.Zero);
        Assert.True(builder.MeshingTime > TimeSpan.Zero);
    }

    [Fact]
    public void Constructor_NullArgument_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new WorldBuilder(null!, BlockPalette.Game, default));
        Assert.Throws<ArgumentNullException>(() => new WorldBuilder(_smallWorld, null!, default));
    }
}
