// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.WorldGeneration;

/// <summary>
/// Gives the limits of the chunk work of the main thread in each frame (#6).
/// </summary>
/// <remarks>
/// The default values keep the chunk work of a frame under 4 ms on lavapipe with 2 CPU cores.
/// A settings file will change them later (#73). The reference computer of #44 can need other values.
/// </remarks>
public sealed record ChunkUploadSettings
{
    /// <summary>
    /// Gets the default limits.
    /// </summary>
    public static ChunkUploadSettings Default { get; } = new();

    /// <summary>
    /// Gets the maximum number of chunk meshes that the main thread uploads to the GPU in one frame. Minimum: 1.
    /// </summary>
    public int MaxChunksPerFrame { get; init; } = 32;

    /// <summary>
    /// Gets the maximum time of the uploads in one frame, in milliseconds.
    /// The budget calculates the number of chunks from the time of the last uploads. A frame always uploads one chunk or more.
    /// </summary>
    public double MaxUploadMillisecondsPerFrame { get; init; } = 3.0;

    /// <summary>
    /// Gets the maximum number of worker results that the main thread receives in one frame. Minimum: 1.
    /// Each generated chunk makes snapshots for the meshes of the chunk and of its neighbors on the main thread.
    /// </summary>
    public int MaxResultsPerFrame { get; init; } = 64;

    /// <summary>
    /// Gets the maximum time to receive the worker results in one frame, in milliseconds.
    /// The main thread receives the results in groups of <see cref="ResultGroupSize"/>, and stops after this time.
    /// </summary>
    public double MaxResultMillisecondsPerFrame { get; init; } = 2.0;

    /// <summary>
    /// Gets the number of results in one group. The main thread examines the time after each group. Minimum: 1.
    /// </summary>
    public int ResultGroupSize { get; init; } = 8;

    /// <summary>
    /// Checks the values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside its range.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxChunksPerFrame, 1, nameof(MaxChunksPerFrame));
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxResultsPerFrame, 1, nameof(MaxResultsPerFrame));
        ArgumentOutOfRangeException.ThrowIfLessThan(ResultGroupSize, 1, nameof(ResultGroupSize));
        ThrowIfNotPositive(MaxUploadMillisecondsPerFrame, nameof(MaxUploadMillisecondsPerFrame));
        ThrowIfNotPositive(MaxResultMillisecondsPerFrame, nameof(MaxResultMillisecondsPerFrame));
    }

    private static void ThrowIfNotPositive(double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "The time must be a positive finite number.");
        }
    }
}
