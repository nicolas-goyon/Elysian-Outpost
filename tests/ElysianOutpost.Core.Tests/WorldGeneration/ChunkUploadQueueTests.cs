// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Core.Tests.WorldGeneration;

public sealed class ChunkUploadQueueTests
{
    private static readonly int3 _chunkSize = new(16, 64, 16);

    [Fact]
    public void TakeNearest_GivesTheChunksNearestToTheCamera_TheNearestFirst()
    {
        var queue = new ChunkUploadQueue(_chunkSize);
        queue.Add([Chunk(64, 0, 0), Chunk(-16, 0, 0), Chunk(0, 0, 160), Chunk(16, 0, 0), Chunk(-160, 0, -160)]);
        var output = new List<BuiltChunk>();

        // The camera is at the center of the chunk (0, 0, 0): the distance to a chunk is the distance to its center.
        int taken = queue.TakeNearest(new float3(8, 32, 8), 3, output);

        Assert.Equal(3, taken);
        Assert.Equal(2, queue.Count);
        // The chunks at -16 and 16 have the same distance. Their order is not specified.
        Assert.Equal([-16, 16], output.Take(2).Select(chunk => chunk.Position.X).Order());
        Assert.Equal(new int3(64, 0, 0), output[2].Position);
    }

    [Fact]
    public void TakeNearest_RepeatedCalls_GiveAllTheChunksInTheOrderOfTheirDistance()
    {
        var queue = new ChunkUploadQueue(_chunkSize);
        var random = new Random(5);
        var chunks = new List<BuiltChunk>();
        for (int i = 0; i < 200; i++)
        {
            chunks.Add(Chunk(16 * random.Next(-20, 20), 64 * random.Next(0, 2), 16 * random.Next(-20, 20)));
        }

        queue.Add(chunks.ToArray());
        int distinct = chunks.Select(chunk => chunk.Position).Distinct().Count();
        var camera = new float3(30, 40, -50);
        var output = new List<BuiltChunk>();
        while (queue.TakeNearest(camera, 7, output) > 0)
        {
        }

        Assert.Equal(distinct, output.Count);
        float[] distances = output.Select(chunk => Distance(chunk.Position, camera)).ToArray();
        Assert.Equal(distances.Order(), distances);
    }

    // A newer mesh of a chunk replaces the older mesh: the queue never uploads an old mesh after a newer one.
    [Fact]
    public void Add_SameChunkTwoTimes_KeepsTheNewerMesh()
    {
        var queue = new ChunkUploadQueue(_chunkSize);
        BuiltChunk older = Chunk(0, 0, 0);
        BuiltChunk newer = Chunk(0, 0, 0, quads: 2);
        queue.Add([older]);
        queue.Add([newer]);
        var output = new List<BuiltChunk>();

        queue.TakeNearest(default, 10, output);

        BuiltChunk taken = Assert.Single(output);
        Assert.Same(newer.Mesh, taken.Mesh);
    }

    // The mesh of an edit goes to the GPU now. The older mesh in the queue must not replace it later.
    [Fact]
    public void Remove_DropsTheMeshOfTheChunk()
    {
        var queue = new ChunkUploadQueue(_chunkSize);
        queue.Add([Chunk(0, 0, 0), Chunk(16, 0, 0)]);

        Assert.True(queue.Remove(new int3(0, 0, 0)));
        Assert.False(queue.Remove(new int3(0, 0, 0)));
        var output = new List<BuiltChunk>();
        queue.TakeNearest(default, 10, output);

        Assert.Equal(new int3(16, 0, 0), Assert.Single(output).Position);
    }

    [Fact]
    public void TakeNearest_EmptyQueueOrZeroCount_GivesNothing()
    {
        var queue = new ChunkUploadQueue(_chunkSize);
        var output = new List<BuiltChunk>();

        Assert.Equal(0, queue.TakeNearest(default, 5, output));
        queue.Add([Chunk(0, 0, 0)]);
        Assert.Equal(0, queue.TakeNearest(default, 0, output));
        Assert.Empty(output);
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.TakeNearest(default, -1, output));
    }

    private static float Distance(int3 position, float3 camera)
    {
        float3 offset = new float3(position.X + 8, position.Y + 32, position.Z + 8) - camera;
        return (offset.X * offset.X) + (offset.Y * offset.Y) + (offset.Z * offset.Z);
    }

    private static BuiltChunk Chunk(int x, int y, int z, int quads = 1)
    {
        var list = new List<MeshQuad>();
        for (int i = 0; i < quads; i++)
        {
            list.Add(new MeshQuad
            {
                Vertex0 = new System.Numerics.Vector3(i, 0, 0),
                Vertex1 = new System.Numerics.Vector3(i, 1, 0),
                Vertex2 = new System.Numerics.Vector3(i + 1, 1, 0),
                Vertex3 = new System.Numerics.Vector3(i + 1, 0, 0),
                Normal = new System.Numerics.Vector3(0, 0, -1),
                VoxelID = 22,
            });
        }

        return new BuiltChunk(new int3(x, y, z), ChunkMeshData.Build(list, BlockPalette.Game));
    }
}
