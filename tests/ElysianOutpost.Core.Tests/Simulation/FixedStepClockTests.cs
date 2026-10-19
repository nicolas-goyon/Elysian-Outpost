// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Simulation;

namespace ElysianOutpost.Core.Tests.Simulation;

public sealed class FixedStepClockTests
{
    [Fact]
    public void Advance_LessThanOneTick_GivesNoTick()
    {
        var clock = new FixedStepClock(ticksPerSecond: 20, maxTicksPerFrame: 5);

        Assert.Equal(0, clock.Advance(TimeSpan.Zero));
        Assert.Equal(0, clock.Advance(TimeSpan.FromMilliseconds(49)));
        Assert.Equal(0, clock.TickCount);
    }

    [Fact]
    public void Advance_KeepsTheRemainderForTheNextFrames()
    {
        var clock = new FixedStepClock(ticksPerSecond: 20, maxTicksPerFrame: 5);

        Assert.Equal(0, clock.Advance(TimeSpan.FromMilliseconds(30)));
        Assert.Equal(1, clock.Advance(TimeSpan.FromMilliseconds(30)));
        Assert.Equal(1, clock.Advance(TimeSpan.FromMilliseconds(40)));
        Assert.Equal(2, clock.TickCount);
    }

    // 10 seconds in frames of 7 milliseconds. The frames do not divide the tick duration, and the result stays exact.
    [Theory]
    [InlineData(20)]
    [InlineData(60)]
    [InlineData(144)]
    public void Advance_ManySmallFrames_GivesTheExactNumberOfTicks(int ticksPerSecond)
    {
        var clock = new FixedStepClock(ticksPerSecond, maxTicksPerFrame: 5);
        var frame = TimeSpan.FromMilliseconds(7);
        var total = TimeSpan.FromSeconds(10);

        for (TimeSpan time = TimeSpan.Zero; time < total; time += frame)
        {
            clock.Advance(time + frame <= total ? frame : total - time);
        }

        Assert.Equal(10L * ticksPerSecond, clock.TickCount);
        Assert.Equal(0, clock.DroppedTickCount);
    }

    [Fact]
    public void Advance_SlowFrame_GivesTheLimitAndDropsTheOtherTicks()
    {
        var clock = new FixedStepClock(ticksPerSecond: 20, maxTicksPerFrame: 5);

        Assert.Equal(5, clock.Advance(TimeSpan.FromSeconds(1)));
        Assert.Equal(5, clock.TickCount);
        Assert.Equal(15, clock.DroppedTickCount);
    }

    [Fact]
    public void Advance_FrameAfterASlowFrame_DoesNotCatchUp()
    {
        var clock = new FixedStepClock(ticksPerSecond: 20, maxTicksPerFrame: 5);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1, clock.Advance(TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void Advance_NegativeTime_ThrowsArgumentOutOfRangeException()
    {
        var clock = new FixedStepClock(ticksPerSecond: 20, maxTicksPerFrame: 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromMilliseconds(-1)));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-20, 5)]
    [InlineData(20, 0)]
    public void Constructor_ValueOfZeroOrLess_ThrowsArgumentOutOfRangeException(int ticksPerSecond, int maxTicksPerFrame) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(ticksPerSecond, maxTicksPerFrame));
}
