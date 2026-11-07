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
    // The builder meshes a chunk when all its neighbors are generated (#48). Thus each mesh is the mesh in the complete world.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void TryBuildNext_GivesTheMeshDataOfTheChunkInTheCompleteWorld(int radiusInChunks)
    {
        var settings = new WorldSettings(seed: 123, chunkSize: new int3(16, 64, 16), radiusInChunks: radiusInChunks, chunkLayers: 1);
        var builder = new WorldBuilder(settings, BlockPalette.Game, default);
        var generation = new MainGeneration(settings.ChunkSize, settings.Seed);
        var completeWorld = new VoxelWorld(settings.ChunkSize);
        foreach (int3 position in settings.GetChunkPositions(default))
        {
            completeWorld.AddChunk(position, generation.GenerateChunkAt(position));
        }

        long quadCount = 0;
        var built = new HashSet<int3>();
        while (builder.TryBuildNext(out BuiltChunk chunk))
        {
            Assert.True(built.Add(chunk.Position), "The builder gave a chunk two times.");
            Assert.True(completeWorld.TryGetChunk(chunk.Position, out ushort[,,]? voxels));
            ChunkMeshData expected = ChunkMeshData.Build(new GreedyMesher().Mesh(voxels, completeWorld, chunk.Position), BlockPalette.Game);
            Assert.Equal(expected.Vertices, chunk.Mesh.Vertices);
            Assert.Equal(expected.Indices, chunk.Mesh.Indices);
            Assert.True(chunk.Mesh.QuadCount > 0);
            quadCount += chunk.Mesh.QuadCount;
        }

        Assert.Equal(quadCount, builder.QuadCount);
        Assert.Equal(settings.GetChunkPositions(default).ToHashSet(), built);
    }

    // The faces between two chunks are hidden (#48). Thus the world has fewer quads than the sum of the chunks meshed alone.
    [Fact]
    public void TryBuildNext_MakesFewerQuadsThanTheChunksMeshedAlone()
    {
        var settings = new WorldSettings(seed: 123, chunkSize: new int3(16, 64, 16), radiusInChunks: 3, chunkLayers: 1);
        var builder = new WorldBuilder(settings, BlockPalette.Game, default);
        var generation = new MainGeneration(settings.ChunkSize, settings.Seed);
        long alone = 0;
        while (builder.TryBuildNext(out BuiltChunk chunk))
        {
            alone += new GreedyMesher().Mesh(generation.GenerateChunkAt(chunk.Position)).Count;
        }

        Assert.True(builder.QuadCount < alone, $"With the neighbors: {builder.QuadCount} quads. Alone: {alone} quads.");
    }

    [Fact]
    public void TryBuildNext_AddsTheVoxelsOfEachChunkToTheVoxelWorld()
    {
        var builder = new WorldBuilder(_smallWorld, BlockPalette.Game, default);
        var generation = new MainGeneration(_smallWorld.ChunkSize, _smallWorld.Seed);
        Assert.Equal(_smallWorld.ChunkSize, builder.Voxels.ChunkSize);

        while (builder.TryBuildNext(out BuiltChunk chunk))
        {
            // The builder generates the chunks before their mesh. Thus the world can have more chunks than the builder gave.
            Assert.True(builder.Voxels.ChunkCount >= builder.BuiltChunkCount);
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
    public void RebuildChunk_GivesTheMeshOfTheChangedVoxels()
    {
        var builder = new WorldBuilder(_smallWorld, BlockPalette.Game, default);
        Assert.True(builder.TryBuildNext(out BuiltChunk built));

        // Without a change, the mesh is the same.
        BuiltChunk same = builder.RebuildChunk(built.Position);
        Assert.Equal(built.Position, same.Position);
        Assert.Equal(built.Mesh.Vertices, same.Mesh.Vertices);

        // A change of the voxels changes the mesh in the same way as the mesher.
        int3 voxel = built.Position + new int3(3, 63, 5);
        Assert.True(builder.Voxels.TrySetVoxel(voxel, 22, out int3 chunk));
        Assert.Equal(built.Position, chunk);
        BuiltChunk changed = builder.RebuildChunk(chunk);
        Assert.True(builder.Voxels.TryGetChunk(chunk, out ushort[,,]? voxels));
        var expected = ChunkMeshData.Build(new GreedyMesher().Mesh(voxels, builder.Voxels, chunk), BlockPalette.Game);
        Assert.Equal(expected.Vertices, changed.Mesh.Vertices);
        Assert.Equal(expected.Indices, changed.Mesh.Indices);
        Assert.NotEqual(built.Mesh.QuadCount, changed.Mesh.QuadCount);
    }

    [Fact]
    public void RebuildChunk_OfAChunkThatTheBuilderDidNotMake_Throws()
    {
        var builder = new WorldBuilder(_smallWorld, BlockPalette.Game, default);
        Assert.Throws<ArgumentException>(() => builder.RebuildChunk(default));
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
