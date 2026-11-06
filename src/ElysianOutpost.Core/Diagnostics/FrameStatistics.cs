// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// Measures the frame rate and the average duration of the parts of a frame, for a debug overlay.
/// </summary>
/// <remarks>
/// The values change once in each period. Thus a person can read them on the screen.
/// The statistics also keep the frame times of the last frames for a graph.
/// The statistics do not allocate memory after the constructor.
/// </remarks>
public sealed class FrameStatistics
{
    private readonly double _periodSeconds;
    private double _previousFrameStart = double.NaN;
    private double _periodStart;
    private int _periodFrames;
    private double _periodFrameMilliseconds;
    private double _periodMaxFrameMilliseconds;
    private double _periodTickMilliseconds;
    private double _periodOverlayMilliseconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameStatistics"/> class.
    /// </summary>
    /// <param name="period">The time between two changes of the values.</param>
    /// <param name="historyCapacity">The maximum number of frame times in <see cref="FrameTimes"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The period or the capacity is zero or less.</exception>
    public FrameStatistics(TimeSpan period, int historyCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        _periodSeconds = period.TotalSeconds;
        FrameTimes = new SampleHistory(historyCapacity);
    }

    /// <summary>
    /// Gets the frame times. The time of a sample is the start of a frame, in seconds.
    /// The value is the time since the start of the previous frame, in milliseconds.
    /// </summary>
    public SampleHistory FrameTimes { get; }

    /// <summary>
    /// Gets a value that tells if the first period is complete. Before that, all the values are 0.
    /// </summary>
    public bool HasValues { get; private set; }

    /// <summary>
    /// Gets the number of frames per second in the last complete period.
    /// </summary>
    public double FramesPerSecond { get; private set; }

    /// <summary>
    /// Gets the average frame time in the last complete period, in milliseconds.
    /// </summary>
    public double AverageFrameMilliseconds { get; private set; }

    /// <summary>
    /// Gets the longest frame time in the last complete period, in milliseconds.
    /// </summary>
    public double MaxFrameMilliseconds { get; private set; }

    /// <summary>
    /// Gets the average time of the simulation ticks in one frame, in the last complete period, in milliseconds.
    /// </summary>
    public double AverageTickMilliseconds { get; private set; }

    /// <summary>
    /// Gets the average time of the main thread for the debug overlay in one frame, in the last complete period, in milliseconds.
    /// </summary>
    public double AverageOverlayMilliseconds { get; private set; }

    /// <summary>
    /// Adds one frame.
    /// </summary>
    /// <param name="frameStart">The start of the frame, from a clock that does not go back.</param>
    /// <param name="tickTime">The time of the simulation ticks in the frame.</param>
    /// <param name="overlayTime">The time of the main thread for the debug overlay in the frame.</param>
    /// <exception cref="ArgumentOutOfRangeException">The start of the frame is before the start of the previous frame, or a time is less than zero.</exception>
    public void AddFrame(TimeSpan frameStart, TimeSpan tickTime, TimeSpan overlayTime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tickTime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(overlayTime, TimeSpan.Zero);
        double start = frameStart.TotalSeconds;
        if (double.IsNaN(_previousFrameStart))
        {
            // The first frame has no previous frame. Thus it has no frame time.
            _previousFrameStart = start;
            _periodStart = start;
            return;
        }

        if (start < _previousFrameStart)
        {
            throw new ArgumentOutOfRangeException(nameof(frameStart), frameStart, "The start of a frame must not be before the start of the previous frame.");
        }

        double frameMilliseconds = (start - _previousFrameStart) * 1000;
        _previousFrameStart = start;
        FrameTimes.Add(start, frameMilliseconds);

        _periodFrames++;
        _periodFrameMilliseconds += frameMilliseconds;
        _periodMaxFrameMilliseconds = Math.Max(_periodMaxFrameMilliseconds, frameMilliseconds);
        _periodTickMilliseconds += tickTime.TotalMilliseconds;
        _periodOverlayMilliseconds += overlayTime.TotalMilliseconds;

        double elapsed = start - _periodStart;
        if (elapsed < _periodSeconds)
        {
            return;
        }

        HasValues = true;
        FramesPerSecond = _periodFrames / elapsed;
        AverageFrameMilliseconds = _periodFrameMilliseconds / _periodFrames;
        MaxFrameMilliseconds = _periodMaxFrameMilliseconds;
        AverageTickMilliseconds = _periodTickMilliseconds / _periodFrames;
        AverageOverlayMilliseconds = _periodOverlayMilliseconds / _periodFrames;

        _periodStart = start;
        _periodFrames = 0;
        _periodFrameMilliseconds = 0;
        _periodMaxFrameMilliseconds = 0;
        _periodTickMilliseconds = 0;
        _periodOverlayMilliseconds = 0;
    }
}
