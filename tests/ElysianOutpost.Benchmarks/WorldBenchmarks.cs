// SPDX-License-Identifier: MPL-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Benchmarks;

/// <summary>
/// The benchmark of the full default world: generate and mesh the 1024 chunks of the sample scene, as the app does at the start.
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
}
