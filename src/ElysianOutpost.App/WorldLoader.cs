// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal.Logging;
using Brutal.Numerics;
using ElysianOutpost.App.Rendering;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.App;

/// <summary>
/// Loads the chunks of the world with the chunk streamer, and gives their meshes to the renderer (#47).
/// </summary>
/// <remarks>
/// <para>
/// The worker threads of the streamer generate and mesh the chunks. The main thread only receives the results and uploads the meshes.
/// Thus the window events and the frames continue during the world generation.
/// </para>
/// <para>
/// The main loop calls <see cref="Update"/> one time in each frame. The first call requests all the chunks of the world.
/// Each call receives a maximum number of results, because the upload of many chunks in one frame makes the frame long. #6 adds a time budget.
/// </para>
/// </remarks>
/// <param name="streamer">The streamer of the chunks.</param>
/// <param name="positions">The positions of the chunks of the world.</param>
/// <param name="renderer">The renderer that receives the chunk meshes.</param>
/// <param name="log">The log category of the world.</param>
internal sealed class WorldLoader(ChunkStreamer streamer, int3[] positions, VulkanRenderer renderer, LogCategory log)
{
    // The maximum number of results in one frame. In a Release build on lavapipe, 32 uploads take about 5 ms.
    private const int MaxResultsPerFrame = 32;

    private readonly List<BuiltChunk> _batch = [];
    private long _startTimestamp;
    private bool _complete;

    /// <summary>
    /// Requests the chunks at the first call, then receives the finished meshes and gives them to the renderer.
    /// </summary>
    public void Update()
    {
        if (_startTimestamp == 0)
        {
            _startTimestamp = Stopwatch.GetTimestamp();
            log.Info($"The world generation starts: {positions.Length} chunks, {streamer.WorkerCount} worker threads.");
            foreach (int3 position in positions)
            {
                streamer.Request(position);
            }
        }

        streamer.Update(_batch, MaxResultsPerFrame);
        if (_batch.Count > 0)
        {
            renderer.AddChunks(CollectionsMarshal.AsSpan(_batch));
            _batch.Clear();
        }

        // The streamer is idle when all the jobs are complete. A failed job gives no mesh, thus the count can stay below the number of chunks.
        if (!_complete && (streamer.DeliveredCount == positions.Length || streamer.IsIdle))
        {
            _complete = true;
            if (streamer.DeliveredCount < positions.Length)
            {
                log.Error($"{positions.Length - streamer.DeliveredCount} of {positions.Length} chunks have no mesh. The log shows the failed jobs.");
            }

            log.Info($"The world is ready: {streamer.DeliveredCount} chunks in {Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds:F2} s. Worker time: generation {streamer.GenerationTime.TotalSeconds:F2} s, meshing {streamer.MeshingTime.TotalSeconds:F2} s. Upload to the GPU: {renderer.ChunkUploadTime.TotalSeconds:F2} s.");
        }
    }

    /// <summary>
    /// Writes a log line if the world is not complete, for example when the window closes during the world generation.
    /// Call this method one time, after the main loop.
    /// </summary>
    public void LogIfIncomplete()
    {
        if (!_complete)
        {
            log.Info($"The window closed before the end of the world generation: {streamer.DeliveredCount} of {positions.Length} chunks.");
        }
    }
}
