// SPDX-License-Identifier: MPL-2.0

using System.Globalization;

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// Keeps the <see cref="FrameSample"/> values of a performance profile, writes them as CSV, and calculates a summary (#80).
/// </summary>
/// <remarks>
/// <para>
/// The profile keeps the samples in memory, and writes the file only at the end. Thus a frame does no file operation.
/// One sample has approximately 100 bytes: one hour at 100 frames each second takes approximately 36 MB.
/// </para>
/// <para>
/// The CSV file has one header line, then one line for each frame. The separator is a comma, and the decimal separator is a period.
/// <c>docs/perf/README.md</c> describes the columns.
/// </para>
/// </remarks>
public sealed class FrameProfile
{
    /// <summary>
    /// The header line of the CSV file.
    /// </summary>
    public const string CsvHeader = "time_ms,frame_ms,events_ms,update_ms,tick_ms,ticks,world_ms,render_ms,gpu_ms,managed_bytes,gc0,gc1,gc2,gpu_bytes,chunk_queue";

    private readonly List<FrameSample> _samples = [];

    /// <summary>
    /// Gets the number of frames in the profile.
    /// </summary>
    public int Count => _samples.Count;

    /// <summary>
    /// Adds the values of a frame.
    /// </summary>
    /// <param name="sample">The values.</param>
    public void Add(in FrameSample sample) => _samples.Add(sample);

    /// <summary>
    /// Writes the profile as CSV text.
    /// </summary>
    /// <param name="writer">The writer that receives the text.</param>
    public void WriteCsv(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine(CsvHeader);
        foreach (FrameSample s in _samples)
        {
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{s.TimeMilliseconds:F3},{s.FrameMilliseconds:F3},{s.EventsMilliseconds:F3},{s.UpdateMilliseconds:F3},{s.TickMilliseconds:F3},{s.TickCount},{s.WorldMilliseconds:F3},{s.RenderMilliseconds:F3},{(s.GpuMilliseconds >= 0 ? s.GpuMilliseconds.ToString("F3", CultureInfo.InvariantCulture) : string.Empty)},{s.ManagedBytes},{s.Gen0Collections},{s.Gen1Collections},{s.Gen2Collections},{s.GpuBytes},{s.ChunkQueueLength}"));
        }
    }

    /// <summary>
    /// Calculates the summary of the frame times.
    /// </summary>
    /// <returns>The summary, or <c>null</c> if the profile has no frame.</returns>
    public ProfileSummary? Summarize()
    {
        if (_samples.Count == 0)
        {
            return null;
        }

        double[] frames = new double[_samples.Count];
        double gpuSum = 0;
        int gpuCount = 0;
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = _samples[i].FrameMilliseconds;
            if (_samples[i].GpuMilliseconds >= 0)
            {
                gpuSum += _samples[i].GpuMilliseconds;
                gpuCount++;
            }
        }

        Array.Sort(frames);
        int n = frames.Length;
        double median = n % 2 == 1 ? frames[n / 2] : (frames[(n / 2) - 1] + frames[n / 2]) / 2;

        // The 1 % low is the mean of the slowest 1 % of the frames, and at least one frame.
        int slowCount = Math.Max(1, n / 100);
        double slowSum = 0;
        for (int i = n - slowCount; i < n; i++)
        {
            slowSum += frames[i];
        }

        FrameSample first = _samples[0];
        FrameSample last = _samples[^1];
        return new ProfileSummary(
            n,
            TimeSpan.FromMilliseconds(last.TimeMilliseconds + last.FrameMilliseconds - first.TimeMilliseconds),
            frames.Average(),
            median,
            slowSum / slowCount,
            frames[^1],
            gpuCount > 0 ? gpuSum / gpuCount : null,
            last.Gen0Collections - first.Gen0Collections,
            last.Gen1Collections - first.Gen1Collections,
            last.Gen2Collections - first.Gen2Collections);
    }
}

/// <summary>
/// The summary of the frame times of a <see cref="FrameProfile"/>. All times are in milliseconds.
/// </summary>
/// <param name="FrameCount">The number of frames.</param>
/// <param name="Duration">The time from the start of the first frame to the end of the last frame.</param>
/// <param name="AverageMilliseconds">The mean frame time.</param>
/// <param name="MedianMilliseconds">The median frame time.</param>
/// <param name="OnePercentLowMilliseconds">The mean frame time of the slowest 1 % of the frames (at least one frame).</param>
/// <param name="MaxMilliseconds">The longest frame time.</param>
/// <param name="AverageGpuMilliseconds">The mean GPU time, or <c>null</c> if the GPU time is not known.</param>
/// <param name="Gen0Collections">The number of garbage collections of generation 0 during the profile.</param>
/// <param name="Gen1Collections">The number of garbage collections of generation 1 during the profile.</param>
/// <param name="Gen2Collections">The number of garbage collections of generation 2 during the profile.</param>
public sealed record ProfileSummary(
    int FrameCount,
    TimeSpan Duration,
    double AverageMilliseconds,
    double MedianMilliseconds,
    double OnePercentLowMilliseconds,
    double MaxMilliseconds,
    double? AverageGpuMilliseconds,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections)
{
    /// <inheritdoc/>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{FrameCount} frames in {Duration.TotalSeconds:F1} s. Frame time: average {AverageMilliseconds:F2} ms, median {MedianMilliseconds:F2} ms, 1 % low {OnePercentLowMilliseconds:F2} ms, maximum {MaxMilliseconds:F2} ms. GPU time: {(AverageGpuMilliseconds is double gpu ? $"average {gpu:F2} ms" : "not known")}. Garbage collections: {Gen0Collections} / {Gen1Collections} / {Gen2Collections} (generations 0 / 1 / 2).");
}
