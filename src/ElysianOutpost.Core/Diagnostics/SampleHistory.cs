// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// Keeps the last samples of a value and the time of each sample, for example the frame times of the last seconds.
/// </summary>
/// <remarks>
/// The history has a fixed capacity. When the history is full, a new sample replaces the oldest sample.
/// Each sample is in the arrays two times: at the index i and at the index i + <see cref="Capacity"/>.
/// Thus the samples of a time window are always contiguous, and a graph can read them without a copy.
/// The history does not allocate memory after the constructor.
/// </remarks>
public sealed class SampleHistory
{
    private readonly double[] _times;
    private readonly double[] _values;

    // The index of the next sample, from 0 to Capacity - 1.
    private int _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleHistory"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of samples.</param>
    /// <exception cref="ArgumentOutOfRangeException">The capacity is zero or less.</exception>
    public SampleHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
        _times = new double[2 * capacity];
        _values = new double[2 * capacity];
    }

    /// <summary>
    /// Gets the maximum number of samples.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the number of samples in the history.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Adds a sample. If the history is full, the sample replaces the oldest sample.
    /// </summary>
    /// <param name="time">The time of the sample. It must not be less than the time of the previous sample.</param>
    /// <param name="value">The value of the sample.</param>
    /// <exception cref="ArgumentOutOfRangeException">The time is not a number, or it is less than the time of the previous sample.</exception>
    public void Add(double time, double value)
    {
        int newest = _next == 0 ? Capacity - 1 : _next - 1;
        if (double.IsNaN(time) || (Count > 0 && time < _times[newest]))
        {
            throw new ArgumentOutOfRangeException(nameof(time), time, "The time must be a number, and it must not be less than the time of the previous sample.");
        }

        _times[_next] = time;
        _times[_next + Capacity] = time;
        _values[_next] = value;
        _values[_next + Capacity] = value;
        _next = _next == Capacity - 1 ? 0 : _next + 1;
        Count = Math.Min(Count + 1, Capacity);
    }

    /// <summary>
    /// Removes all the samples.
    /// </summary>
    public void Clear()
    {
        _next = 0;
        Count = 0;
    }

    /// <summary>
    /// Gets the samples from a start time to the newest sample, in the order of time.
    /// </summary>
    /// <param name="startTime">The start time. The window contains the samples with a time that is equal to or more than this time.</param>
    /// <returns>The samples in the window. Do not keep the spans of the window after the next call of <see cref="Add"/> or <see cref="Clear"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The start time is not a number.</exception>
    public SampleWindow GetWindow(double startTime)
    {
        if (double.IsNaN(startTime))
        {
            throw new ArgumentOutOfRangeException(nameof(startTime), startTime, "The start time must be a number.");
        }

        int oldest = _next - Count;
        if (oldest < 0)
        {
            oldest += Capacity;
        }

        ReadOnlySpan<double> times = _times.AsSpan(oldest, Count);

        // The times are in increasing order. Thus a binary search finds the first sample at or after the start time.
        int low = 0;
        int high = times.Length;
        while (low < high)
        {
            int middle = (low + high) >>> 1;
            if (times[middle] < startTime)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return new SampleWindow(times[low..], _values.AsSpan(oldest + low, Count - low));
    }
}
