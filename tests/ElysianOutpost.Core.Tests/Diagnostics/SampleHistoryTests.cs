// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.Core.Tests.Diagnostics;

public sealed class SampleHistoryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_CapacityZeroOrLess_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleHistory(capacity));
    }

    [Fact]
    public void GetWindow_EmptyHistory_GivesAnEmptyWindow()
    {
        var history = new SampleHistory(4);

        SampleWindow window = history.GetWindow(double.NegativeInfinity);

        Assert.Equal(0, window.Count);
        Assert.Equal(0, window.MaxValue());
    }

    [Fact]
    public void GetWindow_BeforeTheFirstSample_GivesAllTheSamplesInOrder()
    {
        var history = new SampleHistory(4);
        history.Add(1, 10);
        history.Add(2, 20);
        history.Add(3, 30);

        SampleWindow window = history.GetWindow(0);

        Assert.Equal(3, history.Count);
        Assert.Equal([1.0, 2.0, 3.0], window.Times.ToArray());
        Assert.Equal([10.0, 20.0, 30.0], window.Values.ToArray());
    }

    [Fact]
    public void Add_FullHistory_ReplacesTheOldestSample()
    {
        var history = new SampleHistory(3);
        for (int i = 1; i <= 5; i++)
        {
            history.Add(i, i * 10);
        }

        SampleWindow window = history.GetWindow(double.NegativeInfinity);

        Assert.Equal(3, history.Count);
        Assert.Equal([3.0, 4.0, 5.0], window.Times.ToArray());
        Assert.Equal([30.0, 40.0, 50.0], window.Values.ToArray());
    }

    // The window of a full history starts at each position of the arrays. All the windows must be contiguous and in order.
    [Fact]
    public void GetWindow_AtEachPositionOfTheArrays_GivesTheLastSamplesInOrder()
    {
        const int Capacity = 5;
        var history = new SampleHistory(Capacity);
        for (int i = 0; i < 3 * Capacity; i++)
        {
            history.Add(i, -i);

            SampleWindow window = history.GetWindow(double.NegativeInfinity);
            int expectedCount = Math.Min(i + 1, Capacity);
            double[] expectedTimes = Enumerable.Range(i - expectedCount + 1, expectedCount).Select(t => (double)t).ToArray();
            Assert.Equal(expectedTimes, window.Times.ToArray());
            Assert.Equal(expectedTimes.Select(t => -t).ToArray(), window.Values.ToArray());
        }
    }

    [Theory]
    [InlineData(2.5, new[] { 3.0, 4.0 })]
    [InlineData(3.0, new[] { 3.0, 4.0 })]
    [InlineData(4.0, new[] { 4.0 })]
    [InlineData(4.5, new double[0])]
    public void GetWindow_FromAStartTime_GivesTheSamplesAtOrAfterTheStartTime(double startTime, double[] expectedTimes)
    {
        var history = new SampleHistory(3);
        history.Add(1, 0);
        history.Add(2, 0);
        history.Add(3, 0);
        history.Add(4, 0);

        Assert.Equal(expectedTimes, history.GetWindow(startTime).Times.ToArray());
    }

    [Fact]
    public void GetWindow_SamplesWithTheSameTime_GivesAllOfThem()
    {
        var history = new SampleHistory(4);
        history.Add(1, 10);
        history.Add(2, 20);
        history.Add(2, 21);
        history.Add(3, 30);

        Assert.Equal([20.0, 21.0, 30.0], history.GetWindow(2).Values.ToArray());
    }

    [Fact]
    public void Add_TimeBeforeThePreviousSample_Throws()
    {
        var history = new SampleHistory(4);
        history.Add(2, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => history.Add(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => history.Add(double.NaN, 0));
    }

    [Fact]
    public void Add_AfterClear_AcceptsAnEarlierTime()
    {
        var history = new SampleHistory(4);
        history.Add(5, 50);
        history.Clear();

        history.Add(1, 10);

        Assert.Equal(1, history.Count);
        Assert.Equal([1.0], history.GetWindow(0).Times.ToArray());
    }

    [Fact]
    public void GetWindow_NotANumber_Throws()
    {
        var history = new SampleHistory(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => history.GetWindow(double.NaN));
    }

    [Fact]
    public void SampleWindow_TimesAndValuesOfDifferentLengths_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateWindow([1, 2], [1]));
    }

    [Fact]
    public void MaxValue_GivesTheLargestValueOfTheWindow()
    {
        var history = new SampleHistory(8);
        history.Add(1, 100);
        history.Add(2, -5);
        history.Add(3, 7);
        history.Add(4, -2);

        Assert.Equal(7, history.GetWindow(2).MaxValue());
        Assert.Equal(-2, history.GetWindow(4).MaxValue());
    }

    private static void CreateWindow(double[] times, double[] values) => _ = new SampleWindow(times, values);
}
