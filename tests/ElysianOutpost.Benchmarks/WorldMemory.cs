// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Benchmarks;

/// <summary>
/// Measures the managed memory of the full default world after a full garbage collection (#43).
/// </summary>
/// <remarks>
/// The measurement keeps the world builder (with its voxel world) and the mesh data of all the chunks, as the app does before the upload to the GPU.
/// The app releases the mesh data after the upload. Thus the result also gives the memory of the mesh data alone.
/// </remarks>
internal static class WorldMemory
{
    private const double BytesPerMebibyte = 1024 * 1024;

    /// <summary>
    /// Builds the world, then writes the memory values to the console.
    /// </summary>
    public static void Run()
    {
        long before = CollectAndMeasure();
        long start = Stopwatch.GetTimestamp();
        var builder = new WorldBuilder(ChunkData.World, BlockPalette.Game, default);
        var meshes = new List<BuiltChunk>();
        while (builder.TryBuildNext(out BuiltChunk chunk))
        {
            meshes.Add(chunk);
        }

        TimeSpan time = Stopwatch.GetElapsedTime(start);
        long withMeshes = CollectAndMeasure();
        long meshBytes = 0;
        long quads = 0;
        foreach (BuiltChunk chunk in meshes)
        {
            meshBytes += (long)chunk.Mesh.Vertices.Length * Marshal.SizeOf<ChunkVertex>() + ((long)chunk.Mesh.Indices.Length * sizeof(uint));
            quads += chunk.Mesh.QuadCount;
        }

        meshes.Clear();
        long voxelsOnly = CollectAndMeasure();
        GC.KeepAlive(builder);

        Console.WriteLine("Managed memory of the full default world (after a full garbage collection)");
        Console.WriteLine($"  Chunks: {builder.Voxels.ChunkCount}, quads: {quads}, build time on one thread: {time.TotalSeconds:F2} s");
        Console.WriteLine($"  Before the world:                 {before / BytesPerMebibyte,8:F1} MiB");
        Console.WriteLine($"  World builder and all the meshes: {withMeshes / BytesPerMebibyte,8:F1} MiB ({(withMeshes - before) / BytesPerMebibyte:F1} MiB more)");
        Console.WriteLine($"  World builder only (voxel world): {voxelsOnly / BytesPerMebibyte,8:F1} MiB ({(voxelsOnly - before) / BytesPerMebibyte:F1} MiB more)");
        Console.WriteLine($"  Vertex and index data of the meshes: {meshBytes / BytesPerMebibyte:F1} MiB");
    }

    private static long CollectAndMeasure()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        return GC.GetTotalMemory(forceFullCollection: true);
    }
}
