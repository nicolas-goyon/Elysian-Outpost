// SPDX-License-Identifier: MPL-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Brutal.Numerics;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Benchmarks;

/// <summary>
/// The benchmarks of the full default world: generate and mesh the 1024 chunks of the sample scene, as the app does at the start.
/// </summary>
/// <remarks>
/// One operation takes some seconds. Thus the job uses one launch, one warm-up and five measurements.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 1, iterationCount: 5)]
public class WorldBenchmarks
{
    /// <summary>
    /// Generates and meshes all the chunks of the sample scene, on one thread.
    /// </summary>
    /// <returns>The number of quads of the world.</returns>
    [Benchmark]
    public long BuildDefaultWorld()
    {
        var builder = new WorldBuilder(ChunkData.World, BlockPalette.Game, default);
        long quads = 0;
        while (builder.TryBuildNext(out BuiltChunk chunk))
        {
            quads += chunk.Mesh.QuadCount;
        }

        return quads;
    }

    /// <summary>
    /// Gets or sets the number of worker threads of <see cref="StreamDefaultWorld"/>.
    /// </summary>
    [Params(1, 2)]
    public int Workers { get; set; }

    /// <summary>
    /// Generates and meshes all the chunks of the sample scene with the chunk streamer and its worker threads (#47).
    /// The calling thread only receives the results, as the main thread of the app.
    /// </summary>
    /// <returns>The number of quads of the world.</returns>
    [Benchmark]
    public long StreamDefaultWorld()
    {
        using var streamer = new ChunkStreamer(ChunkData.World, BlockPalette.Game, Workers);
        int3[] positions = ChunkData.World.GetChunkPositions(default);
        foreach (int3 position in positions)
        {
            streamer.Request(position);
        }

        var output = new List<BuiltChunk>();
        while (!streamer.IsIdle)
        {
            if (streamer.Update(output) == 0)
            {
                Thread.Yield();
            }
        }

        long quads = 0;
        foreach (BuiltChunk chunk in output)
        {
            quads += chunk.Mesh.QuadCount;
        }

        return quads;
    }
}
