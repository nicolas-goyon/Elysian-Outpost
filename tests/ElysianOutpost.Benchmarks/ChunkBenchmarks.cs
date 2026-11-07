// SPDX-License-Identifier: MPL-2.0

using BenchmarkDotNet.Attributes;
using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Benchmarks;

/// <summary>
/// The benchmarks of one chunk: the terrain generation and the mesher.
/// The mesh benchmarks include the conversion to the GPU vertex data (<see cref="ChunkMeshData.Build"/>), as in the world builder.
/// </summary>
[MemoryDiagnoser]
public class ChunkBenchmarks
{
    private MainGeneration _generation = null!;
    private ushort[,,] _terrain = null!;
    private ushort[,,] _checkerboard = null!;

    /// <summary>
    /// Makes the voxels of the chunks before the measurements.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _generation = new MainGeneration(ChunkData.World.ChunkSize, ChunkData.World.Seed);
        _terrain = _generation.GenerateChunkAt(ChunkData.TerrainChunkPosition);
        _checkerboard = ChunkData.MakeCheckerboard();
    }

    /// <summary>
    /// Generates the voxels of one terrain chunk.
    /// </summary>
    /// <returns>The voxels.</returns>
    [Benchmark]
    public ushort[,,] GenerateChunk() => _generation.GenerateChunkAt(ChunkData.TerrainChunkPosition);

    /// <summary>
    /// Makes the mesh of one terrain chunk.
    /// </summary>
    /// <returns>The mesh data.</returns>
    [Benchmark]
    public ChunkMeshData MeshTerrainChunk() => Mesh(_terrain);

    /// <summary>
    /// Makes the mesh of one checkerboard chunk: the worst case of the mesher.
    /// </summary>
    /// <returns>The mesh data.</returns>
    [Benchmark]
    public ChunkMeshData MeshCheckerboardChunk() => Mesh(_checkerboard);

    private static ChunkMeshData Mesh(ushort[,,] voxels)
    {
        List<MeshQuad> quads = new DisjointSetMeshOptimizer(new Mesh()).Optimize(new Chunk(voxels, default(int3))).Quads;
        return ChunkMeshData.Build(quads, BlockPalette.Game);
    }
}
