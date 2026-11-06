// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Simulation;

/// <summary>
/// Converts the time of each frame into a number of simulation ticks. All the ticks have the same duration.
/// </summary>
/// <remarks>
/// The clock keeps the time that is shorter than one tick for the next frame.
/// If a frame needs more ticks than the limit, the clock drops the ticks after the limit.
/// Thus a slow frame makes the simulation slower. The next frames do not run the dropped ticks.
/// </remarks>
public sealed class FixedStepClock
{
    // The clock counts time in units of 1 / (TimeSpan.TicksPerSecond × TicksPerSecond) seconds.
    // One simulation tick is TimeSpan.TicksPerSecond units. Thus the integer arithmetic is exact for all tick rates.
    private const long UnitsPerTick = TimeSpan.TicksPerSecond;

    private long _pendingUnits;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedStepClock"/> class.
    /// </summary>
    /// <param name="ticksPerSecond">The number of simulation ticks in one second.</param>
    /// <param name="maxTicksPerFrame">The maximum number of simulation ticks in one frame.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is zero or less.</exception>
    public FixedStepClock(int ticksPerSecond, int maxTicksPerFrame)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTicksPerFrame);
        TicksPerSecond = ticksPerSecond;
        MaxTicksPerFrame = maxTicksPerFrame;
    }

    /// <summary>
    /// Gets the number of simulation ticks in one second.
    /// </summary>
    public int TicksPerSecond { get; }

    /// <summary>
    /// Gets the maximum number of simulation ticks in one frame.
    /// </summary>
    public int MaxTicksPerFrame { get; }

    /// <summary>
    /// Gets the total number of simulation ticks that <see cref="Advance"/> gave.
    /// </summary>
    public long TickCount { get; private set; }

    /// <summary>
    /// Gets the total number of simulation ticks that the clock dropped because of the limit.
    /// </summary>
    public long DroppedTickCount { get; private set; }

    /// <summary>
    /// Adds the time of one frame and gives the number of simulation ticks to run in this frame.
    /// </summary>
    /// <param name="elapsed">The time since the previous frame.</param>
    /// <returns>A number from 0 to <see cref="MaxTicksPerFrame"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The time is less than zero.</exception>
    public int Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        _pendingUnits += elapsed.Ticks * TicksPerSecond;
        long dueTicks = _pendingUnits / UnitsPerTick;
        _pendingUnits -= dueTicks * UnitsPerTick;

        int ticks = (int)Math.Min(dueTicks, MaxTicksPerFrame);
        TickCount += ticks;
        DroppedTickCount += dueTicks - ticks;
        return ticks;
    }
}
