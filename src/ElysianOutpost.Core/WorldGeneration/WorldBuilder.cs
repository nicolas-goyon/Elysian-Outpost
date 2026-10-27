// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using Brutal.Numerics;
using ElysianOutpost.Core.Meshing;
using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.WorldGeneration;

/// <summary>
/// Generates and meshes the chunks of a world, one chunk for each call. The caller decides how many chunks to build in each frame.
/// </summary>
/// <remarks>
/// The builder uses the terrain generation of #33 and the mesher of #31. It measures the time of each step.
/// It keeps the voxels of each chunk in <see cref="Voxels"/>, for example for the voxel ray (#40).
/// </remarks>
public sealed class WorldBuilder
{
    private readonly MainGeneration _generation;
    private readonly BlockPalette _palette;
    private readonly int3[] _positions;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldBuilder"/> class.
    /// </summary>
    /// <param name="settings">The settings of the world.</param>
    /// <param name="palette">The palette that gives the colors of the block types.</param>
    /// <param name="center">The center of the world, in voxel coordinates. See <see cref="WorldSettings.GetChunkPositions"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> or <paramref name="palette"/> is <c>null</c>.</exception>
    public WorldBuilder(WorldSettings settings, BlockPalette palette, int3 center)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(palette);
        _generation = new MainGeneration(settings.ChunkSize, settings.Seed);
        _palette = palette;
        _positions = settings.GetChunkPositions(center);
        Voxels = new VoxelWorld(settings.ChunkSize);
    }

    /// <summary>
    /// Gets the voxels of the chunks that the builder made. The builder adds each chunk before <see cref="TryBuildNext"/> returns it.
    /// </summary>
    public VoxelWorld Voxels { get; }

    /// <summary>
    /// Gets the number of chunks of the world.
    /// </summary>
    public int ChunkCount => _positions.Length;

    /// <summary>
    /// Gets the number of chunks that the builder made.
    /// </summary>
    public int BuiltChunkCount { get; private set; }

    /// <summary>
    /// Gets a value that tells if the builder made all the chunks of the world.
    /// </summary>
    public bool IsComplete => BuiltChunkCount == _positions.Length;

    /// <summary>
    /// Gets the number of quads of the chunks that the builder made.
    /// </summary>
    public long QuadCount { get; private set; }

    /// <summary>
    /// Gets the total time of the terrain generation.
    /// </summary>
    public TimeSpan GenerationTime { get; private set; }

    /// <summary>
    /// Gets the total time of the mesher and of the mesh data.
    /// </summary>
    public TimeSpan MeshingTime { get; private set; }

    /// <summary>
    /// Generates and meshes the next chunk, in the order of <see cref="WorldSettings.GetChunkPositions"/>.
    /// </summary>
    /// <param name="chunk">The position and the mesh data of the chunk, if the method returns <c>true</c>.</param>
    /// <returns><c>true</c> if the builder made a chunk. <c>false</c> if the builder made all the chunks before this call.</returns>
    public bool TryBuildNext(out BuiltChunk chunk)
    {
        if (IsComplete)
        {
            chunk = default;
            return false;
        }

        int3 position = _positions[BuiltChunkCount];
        long start = Stopwatch.GetTimestamp();
        ushort[,,] voxels = _generation.GenerateChunkAt(position);
        long generated = Stopwatch.GetTimestamp();
        Voxels.AddChunk(position, voxels);
        List<MeshQuad> quads = new DisjointSetMeshOptimizer(new Mesh()).Optimize(new Chunk(voxels, position)).Quads;
        ChunkMeshData mesh = ChunkMeshData.Build(quads, _palette);
        long meshed = Stopwatch.GetTimestamp();

        GenerationTime += Stopwatch.GetElapsedTime(start, generated);
        MeshingTime += Stopwatch.GetElapsedTime(generated, meshed);
        QuadCount += mesh.QuadCount;
        BuiltChunkCount++;
        chunk = new BuiltChunk(position, mesh);
        return true;
    }
}
