// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal.Logging;
using ElysianOutpost.App.Rendering;
using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.App;

/// <summary>
/// Builds the chunks of the world on the main thread, a few chunks in each frame, and gives their meshes to the renderer.
/// </summary>
/// <remarks>
/// The generation of all the chunks takes several seconds. A loop that builds all the chunks before the first frame
/// stops the window events for that time, and Windows then shows "Not responding".
/// Thus the main loop calls <see cref="Update"/> one time in each frame, with a time budget. The chunks show while the world builds.
/// </remarks>
/// <param name="builder">The builder of the chunks.</param>
/// <param name="renderer">The renderer that receives the chunk meshes.</param>
/// <param name="log">The log category of the world.</param>
internal sealed class WorldLoader(WorldBuilder builder, VulkanRenderer renderer, LogCategory log)
{
    private readonly List<BuiltChunk> _batch = [];
    private long _startTimestamp;
    private bool _complete;

    /// <summary>
    /// Builds chunks until the time budget ends or all the chunks are ready, then gives them to the renderer.
    /// The method always builds at least one chunk if one remains.
    /// </summary>
    /// <param name="budget">The time to use for the chunks in this frame. The upload to the GPU is not in this time.</param>
    public void Update(TimeSpan budget)
    {
        if (_complete)
        {
            return;
        }

        if (_startTimestamp == 0)
        {
            _startTimestamp = Stopwatch.GetTimestamp();
            log.Info($"The world generation starts: {builder.ChunkCount} chunks.");
        }

        long start = Stopwatch.GetTimestamp();
        while (builder.TryBuildNext(out BuiltChunk chunk))
        {
            _batch.Add(chunk);
            if (Stopwatch.GetElapsedTime(start) >= budget)
            {
                break;
            }
        }

        renderer.AddChunks(CollectionsMarshal.AsSpan(_batch));
        _batch.Clear();

        if (builder.IsComplete)
        {
            _complete = true;
            log.Info($"The world is ready: {builder.ChunkCount} chunks and {builder.QuadCount} quads in {Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds:F2} s. Generation: {builder.GenerationTime.TotalSeconds:F2} s. Meshing: {builder.MeshingTime.TotalSeconds:F2} s. Upload to the GPU: {renderer.ChunkUploadTime.TotalSeconds:F2} s.");
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
            log.Info($"The window closed before the end of the world generation: {builder.BuiltChunkCount} of {builder.ChunkCount} chunks.");
        }
    }
}
