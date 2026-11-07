// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.WorldGeneration;

namespace ElysianOutpost.Core.Tests.WorldGeneration;

public sealed class ChunkUploadBudgetTests
{
    private static readonly ChunkUploadSettings _settings = new() { MaxChunksPerFrame = 32, MaxUploadMillisecondsPerFrame = 4.0 };

    [Fact]
    public void ChunkCount_BeforeTheFirstUpload_IsOne()
    {
        var budget = new ChunkUploadBudget(_settings);

        Assert.Equal(1, budget.ChunkCount);
        Assert.Equal(0, budget.MillisecondsPerChunk);
    }

    [Fact]
    public void ChunkCount_IsTheTimeBudgetDividedByTheTimeOfOneChunk()
    {
        var budget = new ChunkUploadBudget(_settings);

        budget.Record(4, TimeSpan.FromMilliseconds(2));

        // 0.5 ms for each chunk: 8 chunks in 4 ms.
        Assert.Equal(0.5, budget.MillisecondsPerChunk, 6);
        Assert.Equal(8, budget.ChunkCount);
        Assert.Equal(4, budget.LastChunkCount);
        Assert.Equal(TimeSpan.FromMilliseconds(2), budget.LastTime);
    }

    [Fact]
    public void ChunkCount_StaysFromOneToTheMaximumNumberOfChunks()
    {
        var fast = new ChunkUploadBudget(_settings);
        var slow = new ChunkUploadBudget(_settings);

        fast.Record(10, TimeSpan.FromMilliseconds(0.01));
        slow.Record(1, TimeSpan.FromMilliseconds(50));

        Assert.Equal(32, fast.ChunkCount);
        Assert.Equal(1, slow.ChunkCount);
    }

    // The moving average follows a slower computer in some frames, and a single slow frame does not stop the uploads.
    [Fact]
    public void Record_UsesAMovingAverage()
    {
        var budget = new ChunkUploadBudget(_settings);
        budget.Record(8, TimeSpan.FromMilliseconds(4));
        budget.Record(1, TimeSpan.FromMilliseconds(4.5));

        // 0.5 + 0.25 × (4.5 − 0.5) = 1.5 ms for each chunk.
        Assert.Equal(1.5, budget.MillisecondsPerChunk, 6);
        Assert.Equal(2, budget.ChunkCount);
        for (int i = 0; i < 30; i++)
        {
            budget.Record(budget.ChunkCount, TimeSpan.FromMilliseconds(0.3 * budget.ChunkCount));
        }

        // 4 ms / 0.3 ms = 13.3 chunks.
        Assert.Equal(13, budget.ChunkCount);
    }

    [Fact]
    public void Record_ZeroChunksOrZeroTime_DoesNotStopTheBudget()
    {
        var budget = new ChunkUploadBudget(_settings);

        budget.Record(0, TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, budget.MillisecondsPerChunk);
        budget.Record(5, TimeSpan.Zero);
        Assert.Equal(32, budget.ChunkCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.Record(-1, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(0, 4.0, 64)]
    [InlineData(32, 0.0, 64)]
    [InlineData(32, -1.0, 64)]
    [InlineData(32, double.NaN, 64)]
    [InlineData(32, double.PositiveInfinity, 64)]
    [InlineData(32, 4.0, 0)]
    public void Constructor_InvalidSettings_Throws(int maxChunks, double maxMilliseconds, int maxResults)
    {
        var settings = new ChunkUploadSettings { MaxChunksPerFrame = maxChunks, MaxUploadMillisecondsPerFrame = maxMilliseconds, MaxResultsPerFrame = maxResults };
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkUploadBudget(_settings with { ResultGroupSize = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkUploadBudget(_settings with { MaxResultMillisecondsPerFrame = 0 }));

        Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkUploadBudget(settings));
        Assert.Throws<ArgumentNullException>(() => new ChunkUploadBudget(null!));
    }
}
