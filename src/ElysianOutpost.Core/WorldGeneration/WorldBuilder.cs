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
/// The builder uses the terrain generation of #33 and the greedy mesher of #45. It measures the time of each step.
/// It keeps the voxels of each chunk in <see cref="Voxels"/>, for example for the voxel ray (#40).
/// <para>
/// The mesh of a chunk hides the faces at its borders with the neighbor chunks (#48). Thus the builder generates the chunks before their mesh:
/// it meshes a chunk when all its neighbor chunks of the world are generated. With the order of <see cref="WorldSettings.GetChunkPositions"/>,
/// the generation is approximately one column of chunks before the mesh. Each chunk gets one mesh, and no chunk must be meshed again.
/// At the edge of the world, the voxels outside the world are air.
/// </para>
/// </remarks>
public sealed class WorldBuilder
{
    private readonly MainGeneration _generation;
    private readonly BlockPalette _palette;
    private readonly GreedyMesher _mesher = new();
    private readonly int3[] _positions;
    private readonly HashSet<int3> _plannedPositions;
    private readonly List<int3> _waitingForMesh = [];
    private int _generatedCount;

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
        _generation = new MainGeneration(settings.ChunkSize, settings.Seed, settings.ChunkLayers);
        _palette = palette;
        _positions = settings.GetChunkPositions(center);
        _plannedPositions = [.. _positions];
        Voxels = new VoxelWorld(settings.ChunkSize);
    }

    /// <summary>
    /// Gets the voxels of the chunks that the builder generated. The builder adds each chunk before its mesh.
    /// Thus <see cref="Voxels"/> can have chunks that <see cref="TryBuildNext"/> did not return yet.
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
    /// Meshes the next chunk whose neighbor chunks are all generated. Before, the method generates the chunks that the mesh needs,
    /// in the order of <see cref="WorldSettings.GetChunkPositions"/>.
    /// </summary>
    /// <param name="chunk">The position and the mesh data of the chunk, if the method returns <c>true</c>.</param>
    /// <returns><c>true</c> if the builder made a chunk. <c>false</c> if the builder made all the chunks before this call.</returns>
    /// <remarks>
    /// The first call generates approximately one column of chunks. Thus it takes more time than the next calls.
    /// </remarks>
    public bool TryBuildNext(out BuiltChunk chunk)
    {
        if (IsComplete)
        {
            chunk = default;
            return false;
        }

        int ready;
        while ((ready = _waitingForMesh.FindIndex(AreNeighborsGenerated)) < 0)
        {
            GenerateNext();
        }

        int3 position = _waitingForMesh[ready];
        _waitingForMesh.RemoveAt(ready);
        long start = Stopwatch.GetTimestamp();
        Voxels.TryGetChunk(position, out ushort[,,]? voxels);
        ChunkMeshData mesh = BuildMesh(position, voxels!);
        MeshingTime += Stopwatch.GetElapsedTime(start);
        QuadCount += mesh.QuadCount;
        BuiltChunkCount++;
        chunk = new BuiltChunk(position, mesh);
        return true;
    }

    // Generates the next chunk of the order, and adds it to the voxel world and to the chunks that wait for their mesh.
    private void GenerateNext()
    {
        int3 position = _positions[_generatedCount];
        long start = Stopwatch.GetTimestamp();
        ushort[,,] voxels = _generation.GenerateChunkAt(position);
        GenerationTime += Stopwatch.GetElapsedTime(start);
        Voxels.AddChunk(position, voxels);
        _waitingForMesh.Add(position);
        _generatedCount++;
    }

    // A neighbor chunk that is not in the world does not stop the mesh: its voxels are air.
    private bool AreNeighborsGenerated(int3 position)
    {
        int3 size = Voxels.ChunkSize;
        ReadOnlySpan<int3> offsets =
        [
            new(-size.X, 0, 0), new(size.X, 0, 0),
            new(0, -size.Y, 0), new(0, size.Y, 0),
            new(0, 0, -size.Z), new(0, 0, size.Z),
        ];
        foreach (int3 offset in offsets)
        {
            int3 neighbor = position + offset;
            if (_plannedPositions.Contains(neighbor) && !Voxels.TryGetChunk(neighbor, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Meshes a chunk again from its voxels in <see cref="Voxels"/>, for example after a voxel edit (#41).
    /// An edit on a chunk border also changes the mesh of the neighbor chunk. <see cref="VoxelWorld.GetChunksToMesh"/> gives these chunks.
    /// </summary>
    /// <param name="chunkPosition">The position of the chunk.</param>
    /// <returns>The position and the new mesh data of the chunk.</returns>
    /// <exception cref="ArgumentException">The builder did not make a chunk at this position.</exception>
    public BuiltChunk RebuildChunk(int3 chunkPosition)
    {
        if (!Voxels.TryGetChunk(chunkPosition, out ushort[,,]? voxels))
        {
            throw new ArgumentException($"The builder did not make a chunk at ({chunkPosition.X}, {chunkPosition.Y}, {chunkPosition.Z}).", nameof(chunkPosition));
        }

        return new BuiltChunk(chunkPosition, BuildMesh(chunkPosition, voxels));
    }

    // The greedy mesher of #45, with the neighbor chunks of the voxel world (#48).
    private ChunkMeshData BuildMesh(int3 position, ushort[,,] voxels)
    {
        return ChunkMeshData.Build(_mesher.Mesh(voxels, Voxels, position), _palette);
    }
}
