// SPDX-License-Identifier: MPL-2.0

using Brutal.Numerics;

namespace ElysianOutpost.Core.WorldGeneration;

/// <summary>
/// Keeps the chunk meshes that wait for the GPU upload, and gives them in the order of their distance to the camera (#6).
/// </summary>
/// <remarks>
/// <para>
/// The queue keeps one mesh for each chunk. A newer mesh of a chunk replaces the mesh in the queue.
/// </para>
/// <para>
/// The meshes of the player edits do not go into the queue: the caller uploads them in the same frame.
/// The caller must then call <see cref="Remove"/> for these chunks, because the mesh in the queue is older than the edit.
/// </para>
/// <para>
/// Use the queue from one thread only.
/// </para>
/// </remarks>
public sealed class ChunkUploadQueue
{
    private readonly Dictionary<int3, BuiltChunk> _chunks = [];
    private readonly PriorityQueue<int3, float> _nearest = new();
    private readonly float3 _halfChunk;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkUploadQueue"/> class.
    /// </summary>
    /// <param name="chunkSize">The number of voxels of a chunk on each axis. The distance to a chunk is the distance to its center.</param>
    public ChunkUploadQueue(int3 chunkSize)
    {
        _halfChunk = new float3(chunkSize.X, chunkSize.Y, chunkSize.Z) * 0.5f;
    }

    /// <summary>
    /// Gets the number of chunks in the queue.
    /// </summary>
    public int Count => _chunks.Count;

    /// <summary>
    /// Adds the meshes of chunks to the queue. A mesh replaces the mesh of the same chunk in the queue.
    /// </summary>
    /// <param name="chunks">The chunks.</param>
    public void Add(ReadOnlySpan<BuiltChunk> chunks)
    {
        foreach (BuiltChunk chunk in chunks)
        {
            _chunks[chunk.Position] = chunk;
        }
    }

    /// <summary>
    /// Removes the mesh of a chunk from the queue, if the queue has one.
    /// </summary>
    /// <param name="position">The position of the chunk.</param>
    /// <returns><c>true</c> if the queue had a mesh of the chunk.</returns>
    public bool Remove(int3 position) => _chunks.Remove(position);

    /// <summary>
    /// Removes the chunks nearest to the camera from the queue, and adds them to a list, the nearest first.
    /// </summary>
    /// <param name="camera">The world position of the camera.</param>
    /// <param name="count">The maximum number of chunks.</param>
    /// <param name="output">The list that receives the chunks. The method adds to the list.</param>
    /// <returns>The number of chunks that the method added.</returns>
    public int TakeNearest(float3 camera, int count, List<BuiltChunk> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        count = Math.Min(count, _chunks.Count);
        if (count == 0)
        {
            return 0;
        }

        // A max-heap of the nearest chunks: the priority is the negative distance. The heap keeps only count chunks.
        _nearest.Clear();
        foreach (int3 position in _chunks.Keys)
        {
            float distance = DistanceSquared(position, camera);
            if (_nearest.Count < count)
            {
                _nearest.Enqueue(position, -distance);
            }
            else if (_nearest.TryPeek(out _, out float farthest) && distance < -farthest)
            {
                _nearest.DequeueEnqueue(position, -distance);
            }
        }

        int first = output.Count;
        while (_nearest.TryDequeue(out int3 position, out _))
        {
            output.Add(_chunks[position]);
            _chunks.Remove(position);
        }

        // The heap gives the farthest chunk first.
        output.Reverse(first, count);
        return count;
    }

    private float DistanceSquared(int3 position, float3 camera)
    {
        float3 offset = new float3(position.X, position.Y, position.Z) + _halfChunk - camera;
        return (offset.X * offset.X) + (offset.Y * offset.Y) + (offset.Z * offset.Z);
    }
}
