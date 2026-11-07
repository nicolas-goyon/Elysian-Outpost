// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Core.Tests.WorldGeneration;

public sealed class ChunkStreamerTests
{
    // Small chunks for fast tests: 8 × 64 × 8 voxels, 6 × 6 chunks around the origin. The terrain of seed 123 fills the lower half.
    private static readonly WorldSettings _world = new(seed: 123, chunkSize: new int3(8, 64, 8), radiusInChunks: 3, chunkLayers: 1);
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void Update_AfterTheRequestOfAllTheChunks_GivesTheMeshOfEachChunkInTheCompleteWorld()
    {
        using var streamer = new ChunkStreamer(_world, BlockPalette.Game, workerCount: 2);
        int3[] positions = _world.GetChunkPositions(default);
        foreach (int3 position in positions)
        {
            Assert.True(streamer.Request(position));
        }

        Dictionary<int3, BuiltChunk> meshes = RunUntilIdle(streamer);

        Assert.Equal(positions.Length, streamer.DeliveredCount);
        Assert.Equal(positions.ToHashSet(), meshes.Keys.ToHashSet());
        var builder = new WorldBuilder(_world, BlockPalette.Game, default);
        while (builder.TryBuildNext(out BuiltChunk expected))
        {
            Assert.Equal(expected.Mesh.Vertices, meshes[expected.Position].Mesh.Vertices);
            Assert.Equal(expected.Mesh.Indices, meshes[expected.Position].Mesh.Indices);
            Assert.Equal(ChunkState.Delivered, streamer.GetState(expected.Position));
        }

        Assert.True(streamer.GenerationTime > TimeSpan.Zero);
        Assert.True(streamer.MeshingTime > TimeSpan.Zero);
    }

    [Fact]
    public void Request_TwoTimes_StartsOneGeneration()
    {
        using var streamer = new ChunkStreamer(_world, BlockPalette.Game, workerCount: 1);

        Assert.True(streamer.Request(default));
        Assert.False(streamer.Request(default));
        Assert.Equal(ChunkState.Requested, streamer.GetState(default));
        Assert.Equal(ChunkState.None, streamer.GetState(new int3(8, 0, 0)));

        Dictionary<int3, BuiltChunk> meshes = RunUntilIdle(streamer);
        Assert.Single(meshes);
        Assert.Equal(1, streamer.RequestedCount);
        Assert.Equal(1, streamer.Voxels.ChunkCount);
    }

    // A neighbor that arrives later changes the faces at the border. Thus the first chunk gets a second mesh, without the hidden faces.
    [Fact]
    public void Request_OfANeighborLater_GivesANewMeshOfTheFirstChunk()
    {
        using var streamer = new ChunkStreamer(_world, BlockPalette.Game, workerCount: 1);
        streamer.Request(default);
        BuiltChunk first = RunUntilIdle(streamer)[default];

        streamer.Request(new int3(8, 0, 0));
        Dictionary<int3, BuiltChunk> second = RunUntilIdle(streamer);

        Assert.True(second.ContainsKey(default));
        Assert.True(second[default].Mesh.QuadCount < first.Mesh.QuadCount);
    }

    [Fact]
    public void MeshAfterEdit_GivesTheNewMeshNow_AndTheOldJobsDoNotReplaceIt()
    {
        using var streamer = new ChunkStreamer(_world, BlockPalette.Game, workerCount: 1);
        streamer.Request(default);
        RunUntilIdle(streamer);

        // A voxel high above the terrain: the edit adds 6 faces.
        var voxel = new int3(3, 60, 3);
        Assert.True(streamer.Voxels.TrySetVoxel(voxel, 22, out _));
        var output = new List<BuiltChunk>();
        streamer.MeshAfterEdit([voxel], output);

        BuiltChunk edited = Assert.Single(output);
        Assert.True(streamer.Voxels.TryGetChunk(default, out ushort[,,]? voxels));
        ChunkMeshData expected = ChunkMeshData.Build(new GreedyMesher().Mesh(voxels, streamer.Voxels, default), BlockPalette.Game);
        Assert.Equal(expected.Vertices, edited.Mesh.Vertices);
        Assert.Empty(RunUntilIdle(streamer));
    }

    // The stress test of #47: 10,000 requests and voxel changes from two threads, while the test thread receives the results.
    // At the end, each requested chunk has a mesh, and its last mesh is the mesh of the final world.
    [Fact]
    public void RequestsAndChangesFromTwoThreads_GiveTheMeshesOfTheFinalWorld()
    {
        // The issue asks for 100 runs. Set ELYSIAN_STRESS_RUNS to 100 for this check. The normal test run uses 3 runs.
        int runs = int.TryParse(Environment.GetEnvironmentVariable("ELYSIAN_STRESS_RUNS"), out int value) ? value : 3;
        for (int run = 0; run < runs; run++)
        {
            RunStress(seed: run);
        }
    }

    // A worker stops the job of the first change. The job of the second change ends first. The old result arrives last,
    // and the streamer must ignore it: its snapshot does not have the second change.
    [Fact]
    public void Update_OldResultThatArrivesLast_DoesNotReplaceTheNewerMesh()
    {
        using var streamer = new ChunkStreamer(_world, BlockPalette.Game, workerCount: 2);
        streamer.Request(default);
        RunUntilIdle(streamer);

        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int heldVersion = -1;
        streamer.MeshJobStarting = (position, version) =>
        {
            if (Interlocked.CompareExchange(ref heldVersion, version, -1) == -1)
            {
                held.Set();
                release.Wait(_timeout);
            }
        };

        // Two voxels high above the terrain. The first job waits in the hook. The second job runs on the other worker.
        Assert.True(streamer.SetVoxel(new int3(2, 60, 2), 22));
        Assert.True(held.Wait(_timeout));
        Assert.True(streamer.SetVoxel(new int3(5, 60, 5), 22));
        var meshes = new Dictionary<int3, BuiltChunk>();
        var output = new List<BuiltChunk>();
        var watch = Stopwatch.StartNew();
        while (!meshes.ContainsKey(default))
        {
            Assert.True(watch.Elapsed < _timeout, "The newer mesh did not arrive.");
            streamer.Update(output);
            foreach (BuiltChunk chunk in output)
            {
                meshes[chunk.Position] = chunk;
            }
        }

        release.Set();
        foreach ((int3 position, BuiltChunk chunk) in RunUntilIdle(streamer))
        {
            meshes[position] = chunk;
        }

        // The final mesh has the two voxels: 12 more quads than the terrain. The old result had only the first voxel.
        Assert.True(streamer.Voxels.TryGetChunk(default, out ushort[,,]? voxels));
        ChunkMeshData expected = ChunkMeshData.Build(new GreedyMesher().Mesh(voxels, streamer.Voxels, default), BlockPalette.Game);
        Assert.Equal(expected.Vertices, meshes[default].Mesh.Vertices);
    }

    [Fact]
    public void WorkerException_GoesToTheErrorMethod_AndTheStreamerContinues()
    {
        // A palette with only air and one color: the mesh data of the terrain throws, because the palette has no color for grass.
        BlockPalette palette = BlockPalette.Parse("""{"blockTypes": [{"id": 0, "color": "#00000000"}, {"id": 1, "color": "#FFFFFFFF"}]}""");
        var errors = new ConcurrentQueue<string>();
        using var streamer = new ChunkStreamer(_world, palette, workerCount: 1, onError: (job, exception) => errors.Enqueue($"{job}: {exception.GetType().Name}"));

        streamer.Request(default);
        streamer.Request(new int3(8, 0, 0));
        Dictionary<int3, BuiltChunk> meshes = RunUntilIdle(streamer);

        Assert.Empty(meshes);
        Assert.Equal(2, streamer.Voxels.ChunkCount);
        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.Contains("Mesh of the chunk", error));
    }

    [Fact]
    public void Dispose_StopsTheWorkers_AndARequestAfterItThrows()
    {
        var streamer = new ChunkStreamer(_world, BlockPalette.Game, workerCount: 2);
        foreach (int3 position in _world.GetChunkPositions(default))
        {
            streamer.Request(position);
        }

        var watch = Stopwatch.StartNew();
        streamer.Dispose();
        streamer.Dispose();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Throws<ObjectDisposedException>(() => streamer.Request(new int3(80, 0, 0)));
    }

    [Fact]
    public void Constructor_WithInvalidArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ChunkStreamer(null!, BlockPalette.Game));
        Assert.Throws<ArgumentNullException>(() => new ChunkStreamer(_world, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkStreamer(_world, BlockPalette.Game, workerCount: 0));
    }

    private static void RunStress(int seed)
    {
        using var streamer = new ChunkStreamer(_world, BlockPalette.Game, workerCount: 2);
        int3[] positions = _world.GetChunkPositions(default);
        var exceptions = new ConcurrentQueue<Exception>();
        var requested = new ConcurrentDictionary<int3, bool>();
        var start = new ManualResetEventSlim();

        var requests = new Thread(() => Guard(exceptions, () =>
        {
            var random = new Random(seed);
            start.Wait();
            for (int i = 0; i < 5000; i++)
            {
                int3 position = positions[random.Next(positions.Length)];
                streamer.Request(position);
                requested[position] = true;
            }
        }));
        var changes = new Thread(() => Guard(exceptions, () =>
        {
            var random = new Random(seed + 1000);
            start.Wait();
            for (int i = 0; i < 5000; i++)
            {
                // A voxel near a chunk border, in the air or in the terrain. Before its chunk is generated, the change does nothing.
                int3 chunk = positions[random.Next(positions.Length)];
                var voxel = new int3(chunk.X + (random.Next(2) * 7), random.Next(20, 50), chunk.Z + random.Next(8));
                streamer.SetVoxel(voxel, random.Next(2) == 0 ? (ushort)0 : (ushort)22);
            }
        }));

        requests.Start();
        changes.Start();
        start.Set();
        var meshes = new Dictionary<int3, BuiltChunk>();
        var output = new List<BuiltChunk>();
        var watch = Stopwatch.StartNew();
        while (requests.IsAlive || changes.IsAlive || !streamer.IsIdle)
        {
            Assert.True(watch.Elapsed < _timeout, "The streamer did not finish in time.");
            output.Clear();
            streamer.Update(output);
            foreach (BuiltChunk chunk in output)
            {
                meshes[chunk.Position] = chunk;
            }
        }

        requests.Join();
        changes.Join();
        Assert.Empty(exceptions);
        Assert.Equal(requested.Keys.ToHashSet(), meshes.Keys.ToHashSet());
        var mesher = new GreedyMesher();
        foreach ((int3 position, BuiltChunk chunk) in meshes)
        {
            Assert.True(streamer.Voxels.TryGetChunk(position, out ushort[,,]? voxels));
            ChunkMeshData expected = ChunkMeshData.Build(mesher.Mesh(voxels, streamer.Voxels, position), BlockPalette.Game);
            Assert.Equal(expected.Vertices, chunk.Mesh.Vertices);
            Assert.Equal(expected.Indices, chunk.Mesh.Indices);
        }
    }

    private static void Guard(ConcurrentQueue<Exception> exceptions, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            exceptions.Enqueue(exception);
        }
    }

    // Receives the results until no job is left. Gives the last mesh of each chunk.
    private static Dictionary<int3, BuiltChunk> RunUntilIdle(ChunkStreamer streamer)
    {
        var meshes = new Dictionary<int3, BuiltChunk>();
        var output = new List<BuiltChunk>();
        var watch = Stopwatch.StartNew();
        while (!streamer.IsIdle)
        {
            Assert.True(watch.Elapsed < _timeout, "The streamer did not finish in time.");
            output.Clear();
            if (streamer.Update(output) == 0)
            {
                Thread.Sleep(1);
            }

            foreach (BuiltChunk chunk in output)
            {
                meshes[chunk.Position] = chunk;
            }
        }

        return meshes;
    }
}
