// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.WorldGeneration;

/// <summary>
/// Calculates the number of chunk meshes to upload in a frame, from a maximum number of chunks and a maximum time (#6).
/// </summary>
/// <remarks>
/// <para>
/// The budget measures the time of each upload, and keeps a moving average of the time for one chunk.
/// The number of chunks of the next frame is the maximum time divided by this average, from 1 to the maximum number of chunks.
/// </para>
/// <para>
/// Before the first measurement, the budget gives one chunk. Each upload has a fixed cost (one GPU submission and one wait).
/// Thus the average of small uploads is high, and the number of chunks increases over the next frames.
/// </para>
/// </remarks>
public sealed class ChunkUploadBudget
{
    // The weight of a new measurement in the moving average.
    private const double Smoothing = 0.25;

    private readonly ChunkUploadSettings _settings;
    private double _millisecondsPerChunk;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkUploadBudget"/> class.
    /// </summary>
    /// <param name="settings">The limits of each frame.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A value of the settings is outside its range.</exception>
    public ChunkUploadBudget(ChunkUploadSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _settings = settings;
    }

    /// <summary>
    /// Gets the limits of each frame.
    /// </summary>
    public ChunkUploadSettings Settings => _settings;

    /// <summary>
    /// Gets the moving average of the upload time of one chunk, in milliseconds. Zero before the first measurement.
    /// </summary>
    public double MillisecondsPerChunk => _millisecondsPerChunk;

    /// <summary>
    /// Gets the number of chunks of the last upload.
    /// </summary>
    public int LastChunkCount { get; private set; }

    /// <summary>
    /// Gets the time of the last upload.
    /// </summary>
    public TimeSpan LastTime { get; private set; }

    /// <summary>
    /// Gets the number of chunks to upload in the next frame: from 1 to the maximum number of chunks.
    /// </summary>
    public int ChunkCount
    {
        get
        {
            if (_millisecondsPerChunk <= 0)
            {
                return 1;
            }

            double count = Math.Floor(_settings.MaxUploadMillisecondsPerFrame / _millisecondsPerChunk);
            return (int)Math.Clamp(count, 1, _settings.MaxChunksPerFrame);
        }
    }

    /// <summary>
    /// Records the time of an upload.
    /// </summary>
    /// <param name="chunkCount">The number of chunks of the upload. Zero records nothing.</param>
    /// <param name="time">The time of the upload.</param>
    public void Record(int chunkCount, TimeSpan time)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chunkCount);
        if (chunkCount == 0)
        {
            return;
        }

        LastChunkCount = chunkCount;
        LastTime = time;
        // A minimum value: a time of zero must not stop the budget at one chunk.
        double perChunk = Math.Max(time.TotalMilliseconds / chunkCount, 1e-6);
        _millisecondsPerChunk = _millisecondsPerChunk <= 0 ? perChunk : _millisecondsPerChunk + (Smoothing * (perChunk - _millisecondsPerChunk));
    }
}
