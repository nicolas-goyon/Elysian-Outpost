// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// The samples of a <see cref="SampleHistory"/> in a time window, from the oldest sample to the newest sample.
/// </summary>
public readonly ref struct SampleWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SampleWindow"/> struct.
    /// </summary>
    /// <param name="times">The times of the samples, in increasing order.</param>
    /// <param name="values">The values of the samples. The length must be the length of <paramref name="times"/>.</param>
    /// <exception cref="ArgumentException">The two spans do not have the same length.</exception>
    public SampleWindow(ReadOnlySpan<double> times, ReadOnlySpan<double> values)
    {
        if (times.Length != values.Length)
        {
            throw new ArgumentException("The times and the values must have the same length.", nameof(values));
        }

        Times = times;
        Values = values;
    }

    /// <summary>
    /// Gets the times of the samples, in increasing order.
    /// </summary>
    public ReadOnlySpan<double> Times { get; }

    /// <summary>
    /// Gets the values of the samples, in the order of <see cref="Times"/>.
    /// </summary>
    public ReadOnlySpan<double> Values { get; }

    /// <summary>
    /// Gets the number of samples in the window.
    /// </summary>
    public int Count => Times.Length;

    /// <summary>
    /// Gets the largest value in the window.
    /// </summary>
    /// <returns>The largest value, or 0 if the window is empty.</returns>
    public double MaxValue()
    {
        if (Values.IsEmpty)
        {
            return 0;
        }

        double max = Values[0];
        foreach (double value in Values)
        {
            max = Math.Max(max, value);
        }

        return max;
    }
}
