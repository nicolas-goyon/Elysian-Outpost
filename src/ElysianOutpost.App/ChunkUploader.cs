// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal.Numerics;
using ElysianOutpost.App.Rendering;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.App;

/// <summary>
/// Uploads the chunk meshes to the GPU in a budget for each frame (#6).
/// </summary>
/// <remarks>
/// <para>
/// The meshes of the world generation go into a queue. Each frame uploads the chunks nearest to the camera,
/// in the limits of <see cref="ChunkUploadSettings"/>. The other chunks wait for the next frames.
/// </para>
/// <para>
/// The meshes of the player edits have the highest priority: <see cref="UploadNow"/> uploads them in the same frame, outside the budget.
/// Thus an edit shows in the next frame, also during the world generation.
/// </para>
/// </remarks>
internal sealed class ChunkUploader
{
    private readonly VulkanRenderer _renderer;
    private readonly ChunkUploadQueue _queue;
    private readonly ChunkUploadBudget _budget;
    private readonly List<BuiltChunk> _batch = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkUploader"/> class.
    /// </summary>
    /// <param name="renderer">The renderer that receives the chunk meshes.</param>
    /// <param name="chunkSize">The number of voxels of a chunk on each axis.</param>
    /// <param name="settings">The limits of each frame.</param>
    public ChunkUploader(VulkanRenderer renderer, int3 chunkSize, ChunkUploadSettings settings)
    {
        _renderer = renderer;
        _queue = new ChunkUploadQueue(chunkSize);
        _budget = new ChunkUploadBudget(settings);
    }

    /// <summary>
    /// Gets the number of chunks that wait for the upload.
    /// </summary>
    public int QueueLength => _queue.Count;

    /// <summary>
    /// Gets the budget of each frame, with the measurements of the last upload.
    /// </summary>
    public ChunkUploadBudget Budget => _budget;

    /// <summary>
    /// Adds the meshes of chunks to the queue. A mesh replaces the older mesh of the same chunk in the queue.
    /// </summary>
    /// <param name="chunks">The chunks.</param>
    public void Enqueue(ReadOnlySpan<BuiltChunk> chunks) => _queue.Add(chunks);

    /// <summary>
    /// Uploads the meshes of chunks now, outside the budget. The meshes of these chunks in the queue are older, thus the queue drops them.
    /// </summary>
    /// <param name="chunks">The chunks, for example the chunks of a player edit.</param>
    public void UploadNow(ReadOnlySpan<BuiltChunk> chunks)
    {
        foreach (BuiltChunk chunk in chunks)
        {
            _queue.Remove(chunk.Position);
        }

        _renderer.AddChunks(chunks);
    }

    /// <summary>
    /// Uploads the chunks nearest to the camera, in the budget of the frame. Call it one time in each frame.
    /// </summary>
    /// <param name="camera">The world position of the camera.</param>
    public void Update(float3 camera)
    {
        _batch.Clear();
        if (_queue.TakeNearest(camera, _budget.ChunkCount, _batch) == 0)
        {
            return;
        }

        long start = Stopwatch.GetTimestamp();
        _renderer.AddChunks(CollectionsMarshal.AsSpan(_batch));
        _budget.Record(_batch.Count, Stopwatch.GetElapsedTime(start));
    }
}
