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
/// Each call receives a maximum number of results, and gives the meshes to the <see cref="ChunkUploader"/>. The uploader limits the uploads of each frame (#6).
/// </para>
/// </remarks>
/// <param name="streamer">The streamer of the chunks.</param>
/// <param name="positions">The positions of the chunks of the world.</param>
/// <param name="uploader">The uploader that receives the chunk meshes.</param>
/// <param name="renderer">The renderer. The loader reads its upload time for the log.</param>
/// <param name="settings">The limits of each frame.</param>
/// <param name="log">The log category of the world.</param>
internal sealed class WorldLoader(ChunkStreamer streamer, int3[] positions, ChunkUploader uploader, VulkanRenderer renderer, ChunkUploadSettings settings, LogCategory log)
{
    private readonly List<BuiltChunk> _batch = [];
    private long _startTimestamp;
    // The chunk work of each frame on the main thread during the world generation, in milliseconds. The log gives its distribution.
    private readonly List<double> _resultTimes = [];
    private readonly List<double> _uploadTimes = [];
    private bool _complete;

    /// <summary>
    /// Requests the chunks at the first call, then receives the finished meshes and uploads the nearest ones in the budget of the frame.
    /// </summary>
    /// <param name="camera">The world position of the camera. The chunks near the camera go to the GPU first.</param>
    public void Update(float3 camera)
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

        long start = Stopwatch.GetTimestamp();
        ReceiveResults();
        uploader.Enqueue(CollectionsMarshal.AsSpan(_batch));
        _batch.Clear();
        long received = Stopwatch.GetTimestamp();
        uploader.Update(camera);
        if (!_complete)
        {
            _resultTimes.Add(Stopwatch.GetElapsedTime(start, received).TotalMilliseconds);
            _uploadTimes.Add(Stopwatch.GetElapsedTime(received).TotalMilliseconds);
        }

        // The streamer is idle when all the jobs are complete. A failed job gives no mesh, thus the count can stay below the number of chunks.
        if (!_complete && (streamer.DeliveredCount == positions.Length || streamer.IsIdle) && uploader.QueueLength == 0)
        {
            _complete = true;
            if (streamer.DeliveredCount < positions.Length)
            {
                log.Error($"{positions.Length - streamer.DeliveredCount} of {positions.Length} chunks have no mesh. The log shows the failed jobs.");
            }

            log.Info($"The world is ready: {streamer.DeliveredCount} chunks in {Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds:F2} s. Worker time: generation {streamer.GenerationTime.TotalSeconds:F2} s, meshing {streamer.MeshingTime.TotalSeconds:F2} s. Upload to the GPU: {renderer.ChunkUploadTime.TotalSeconds:F2} s. Chunk work in each frame: results {Distribution(_resultTimes)}, upload {Distribution(_uploadTimes)}.");
        }
    }

    // The median, the 95th percentile and the maximum.
    private static string Distribution(List<double> times)
    {
        if (times.Count == 0)
        {
            return "no frame";
        }

        times.Sort();
        return $"median {times[times.Count / 2]:F2} ms, 95 % {times[(int)(times.Count * 0.95)]:F2} ms, maximum {times[^1]:F2} ms ({times.Count} frames)";
    }

    // Receives the results in groups, until the maximum number of results or the maximum time of the frame (#6).
    // A chunk can be in two groups. The upload queue keeps only its newer mesh.
    private void ReceiveResults()
    {
        long start = Stopwatch.GetTimestamp();
        int received = 0;
        while (received < settings.MaxResultsPerFrame)
        {
            int group = Math.Min(settings.ResultGroupSize, settings.MaxResultsPerFrame - received);
            int count = streamer.Update(_batch, group);
            received += count;
            if (count < group || Stopwatch.GetElapsedTime(start).TotalMilliseconds >= settings.MaxResultMillisecondsPerFrame)
            {
                return;
            }
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
