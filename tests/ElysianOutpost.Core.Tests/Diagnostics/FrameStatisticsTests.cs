// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.Core.Tests.Diagnostics;

public sealed class FrameStatisticsTests
{
    private static readonly TimeSpan _halfSecond = TimeSpan.FromSeconds(0.5);

    [Fact]
    public void Constructor_PeriodZeroOrLess_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameStatistics(TimeSpan.Zero, historyCapacity: 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameStatistics(_halfSecond, historyCapacity: 0));
    }

    [Fact]
    public void AddFrame_BeforeTheEndOfTheFirstPeriod_HasNoValues()
    {
        var statistics = new FrameStatistics(_halfSecond, historyCapacity: 64);

        AddFrames(statistics, start: TimeSpan.Zero, count: 10, frameTime: TimeSpan.FromMilliseconds(40));

        Assert.False(statistics.HasValues);
        Assert.Equal(0, statistics.FramesPerSecond);
    }

    [Fact]
    public void AddFrame_RegularFrames_GivesTheFrameRateAndTheAverages()
    {
        var statistics = new FrameStatistics(_halfSecond, historyCapacity: 64);

        // The first frame has no frame time. The next 25 frames of 20 ms complete the period of 0.5 s.
        AddFrames(statistics, start: TimeSpan.Zero, count: 26, frameTime: TimeSpan.FromMilliseconds(20), tickTime: TimeSpan.FromMilliseconds(2), overlayTime: TimeSpan.FromMilliseconds(0.5));

        Assert.True(statistics.HasValues);
        Assert.Equal(50, statistics.FramesPerSecond, precision: 9);
        Assert.Equal(20, statistics.AverageFrameMilliseconds, precision: 9);
        Assert.Equal(20, statistics.MaxFrameMilliseconds, precision: 9);
        Assert.Equal(2, statistics.AverageTickMilliseconds, precision: 9);
        Assert.Equal(0.5, statistics.AverageOverlayMilliseconds, precision: 9);
        Assert.Equal(25, statistics.FrameTimes.Count);
    }

    [Fact]
    public void AddFrame_OneSlowFrame_GivesItAsTheMaximum()
    {
        var statistics = new FrameStatistics(_halfSecond, historyCapacity: 64);
        statistics.AddFrame(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        statistics.AddFrame(TimeSpan.FromMilliseconds(100), TimeSpan.Zero, TimeSpan.Zero);
        statistics.AddFrame(TimeSpan.FromMilliseconds(400), TimeSpan.Zero, TimeSpan.Zero);
        statistics.AddFrame(TimeSpan.FromMilliseconds(500), TimeSpan.Zero, TimeSpan.Zero);

        Assert.True(statistics.HasValues);
        Assert.Equal(6, statistics.FramesPerSecond, precision: 9);
        Assert.Equal(500.0 / 3, statistics.AverageFrameMilliseconds, precision: 9);
        Assert.Equal(300, statistics.MaxFrameMilliseconds, precision: 9);
    }

    // The first period has larger values than the second period. Thus the values of the second period show
    // if the statistics forget a value of the first period.
    [Fact]
    public void AddFrame_SecondPeriod_UsesOnlyTheFramesOfTheSecondPeriod()
    {
        var statistics = new FrameStatistics(_halfSecond, historyCapacity: 64);
        AddFrames(statistics, start: TimeSpan.Zero, count: 11, frameTime: TimeSpan.FromMilliseconds(50), tickTime: TimeSpan.FromMilliseconds(3), overlayTime: TimeSpan.FromMilliseconds(0.8));

        // The second period has 25 frames of 20 ms. They start after the last frame of the first period.
        AddFrames(statistics, start: TimeSpan.FromMilliseconds(520), count: 25, frameTime: TimeSpan.FromMilliseconds(20), tickTime: TimeSpan.FromMilliseconds(1), overlayTime: TimeSpan.FromMilliseconds(0.25));

        Assert.Equal(50, statistics.FramesPerSecond, precision: 9);
        Assert.Equal(20, statistics.AverageFrameMilliseconds, precision: 9);
        Assert.Equal(20, statistics.MaxFrameMilliseconds, precision: 9);
        Assert.Equal(1, statistics.AverageTickMilliseconds, precision: 9);
        Assert.Equal(0.25, statistics.AverageOverlayMilliseconds, precision: 9);
    }

    // A period ends at the first frame at or after its nominal end. The frame rate uses the measured duration of the period.
    [Fact]
    public void AddFrame_PeriodLongerThanTheNominalPeriod_UsesTheMeasuredDuration()
    {
        var statistics = new FrameStatistics(_halfSecond, historyCapacity: 64);

        // Two frames of 300 ms: the period has 0.6 s, not 0.5 s.
        AddFrames(statistics, start: TimeSpan.Zero, count: 3, frameTime: TimeSpan.FromMilliseconds(300));

        Assert.True(statistics.HasValues);
        Assert.Equal(2 / 0.6, statistics.FramesPerSecond, precision: 9);
        Assert.Equal(300, statistics.AverageFrameMilliseconds, precision: 9);
    }

    [Fact]
    public void AddFrame_KeepsTheFrameTimesWithTheStartOfEachFrame()
    {
        var statistics = new FrameStatistics(_halfSecond, historyCapacity: 64);
        statistics.AddFrame(TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.Zero);
        statistics.AddFrame(TimeSpan.FromSeconds(1.25), TimeSpan.Zero, TimeSpan.Zero);
        statistics.AddFrame(TimeSpan.FromSeconds(1.75), TimeSpan.Zero, TimeSpan.Zero);

        SampleWindow window = statistics.FrameTimes.GetWindow(double.NegativeInfinity);

        Assert.Equal([1.25, 1.75], window.Times.ToArray());
        Assert.Equal([250.0, 500.0], window.Values.ToArray());
    }

    [Fact]
    public void AddFrame_InvalidTimes_Throws()
    {
        var statistics = new FrameStatistics(_halfSecond, historyCapacity: 64);
        statistics.AddFrame(TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.AddFrame(TimeSpan.FromSeconds(0.5), TimeSpan.Zero, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.AddFrame(TimeSpan.FromSeconds(2), TimeSpan.FromTicks(-1), TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.AddFrame(TimeSpan.FromSeconds(2), TimeSpan.Zero, TimeSpan.FromTicks(-1)));
    }

    // Adds frames that start at "start" and follow at intervals of "frameTime".
    private static void AddFrames(
        FrameStatistics statistics,
        TimeSpan start,
        int count,
        TimeSpan frameTime,
        TimeSpan tickTime = default,
        TimeSpan overlayTime = default)
    {
        for (int i = 0; i < count; i++)
        {
            statistics.AddFrame(start + (i * frameTime), tickTime, overlayTime);
        }
    }
}
