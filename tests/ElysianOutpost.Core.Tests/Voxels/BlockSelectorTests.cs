// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Voxels;

namespace ElysianOutpost.Core.Tests.Voxels;

public sealed class BlockSelectorTests
{
    private static readonly ushort[] _blockTypes = [8, 11, 16, 22];

    [Fact]
    public void Constructor_SelectsTheInitialBlockType()
    {
        var selector = new BlockSelector(_blockTypes, 16);

        Assert.Equal(16, selector.Current);
        Assert.Equal(2, selector.Index);
    }

    [Theory]
    [InlineData(1, 22)]
    [InlineData(2, 8)]
    [InlineData(-1, 11)]
    [InlineData(-3, 22)]
    [InlineData(4, 16)]
    [InlineData(-9, 11)]
    public void Scroll_MovesByOneBlockTypeForEachStep_AndGoesAround(double offset, ushort expected)
    {
        var selector = new BlockSelector(_blockTypes, 16);

        bool changed = selector.Scroll(offset);

        Assert.Equal(expected, selector.Current);
        Assert.Equal(expected != 16, changed);
    }

    [Fact]
    public void Scroll_AddsTheOffsetsLessThanOneStep()
    {
        var selector = new BlockSelector(_blockTypes, 8);

        Assert.False(selector.Scroll(0.4));
        Assert.False(selector.Scroll(0.4));
        Assert.True(selector.Scroll(0.4));
        Assert.Equal(11, selector.Current);

        // 0.2 remains. -0.5 gives -0.3, which is less than one step.
        Assert.False(selector.Scroll(-0.5));
        Assert.True(selector.Scroll(-0.8));
        Assert.Equal(8, selector.Current);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0)]
    public void Scroll_WithNoStep_DoesNothing(double offset)
    {
        var selector = new BlockSelector(_blockTypes, 11);

        Assert.False(selector.Scroll(offset));
        Assert.Equal(11, selector.Current);
        Assert.True(selector.Scroll(1));
        Assert.Equal(16, selector.Current);
    }

    [Theory]
    [InlineData(1e300)]
    [InlineData(-1e300)]
    [InlineData(int.MaxValue + 10.0)]
    public void Scroll_WithAVeryLargeOffset_KeepsAValidIndex(double offset)
    {
        var selector = new BlockSelector(_blockTypes, 11);

        selector.Scroll(offset);

        Assert.InRange(selector.Index, 0, _blockTypes.Length - 1);
        Assert.Contains(selector.Current, _blockTypes);
    }

    [Fact]
    public void Constructor_WithAnInvalidList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BlockSelector(null!, 1));
        Assert.Throws<ArgumentException>(() => new BlockSelector([], 1));
        Assert.Throws<ArgumentException>(() => new BlockSelector([0, 1], 1));
        Assert.Throws<ArgumentException>(() => new BlockSelector([1, 2], 3));
    }
}
