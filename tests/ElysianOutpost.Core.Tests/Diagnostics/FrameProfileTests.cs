// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.Core.Tests.Diagnostics;

public sealed class FrameProfileTests
{
    [Fact]
    public void WriteCsv_GivesTheHeaderAndOneLineForEachFrame_WithInvariantNumbers()
    {
        var profile = new FrameProfile();
        profile.Add(new FrameSample
        {
            TimeMilliseconds = 0,
            FrameMilliseconds = 16.6667,
            EventsMilliseconds = 0.1,
            UpdateMilliseconds = 0.25,
            TickMilliseconds = 0.01,
            TickCount = 1,
            WorldMilliseconds = 1.5,
            RenderMilliseconds = 3.125,
            GpuMilliseconds = 2.5,
            ManagedBytes = 12_345_678,
            Gen0Collections = 4,
            Gen1Collections = 2,
            Gen2Collections = 1,
            GpuBytes = 5_000_000,
            ChunkQueueLength = 17,
        });
        profile.Add(new FrameSample { TimeMilliseconds = 16.6667, FrameMilliseconds = 10, GpuMilliseconds = -1 });
        var writer = new StringWriter();

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            profile.WriteCsv(writer);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        string[] lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Equal(FrameProfile.CsvHeader, lines[0]);
        Assert.Equal("0.000,16.667,0.100,0.250,0.010,1,1.500,3.125,2.500,12345678,4,2,1,5000000,17", lines[1]);

        // An unknown GPU time gives an empty field.
        Assert.Equal("16.667,10.000,0.000,0.000,0.000,0,0.000,0.000,,0,0,0,0,0,0", lines[2]);
        Assert.All(lines, line => Assert.Equal(14, line.Count(c => c == ',')));
    }

    [Fact]
    public void Summarize_GivesTheAverageTheMedianTheOnePercentLowAndTheMaximum()
    {
        var profile = new FrameProfile();
        double time = 0;

        // 198 frames of 10 ms, one of 50 ms and one of 100 ms. 1 % of 200 frames is 2 frames: the 1 % low is (50 + 100) / 2.
        for (int i = 0; i < 200; i++)
        {
            double frame = i == 50 ? 100 : i == 120 ? 50 : 10;
            profile.Add(new FrameSample { TimeMilliseconds = time, FrameMilliseconds = frame, GpuMilliseconds = i < 100 ? 4 : -1, Gen0Collections = i / 10, Gen2Collections = 3 });
            time += frame;
        }

        ProfileSummary summary = profile.Summarize()!;

        Assert.Equal(200, summary.FrameCount);
        Assert.Equal(TimeSpan.FromMilliseconds(2130), summary.Duration);
        Assert.Equal(10.65, summary.AverageMilliseconds, 9);
        Assert.Equal(10, summary.MedianMilliseconds);
        Assert.Equal(75, summary.OnePercentLowMilliseconds);
        Assert.Equal(100, summary.MaxMilliseconds);
        Assert.Equal(4, summary.AverageGpuMilliseconds);
        Assert.Equal(19, summary.Gen0Collections);
        Assert.Equal(0, summary.Gen2Collections);
        Assert.Contains("1 % low 75.00 ms", summary.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Summarize_FewFramesOrNoFrame()
    {
        var profile = new FrameProfile();
        Assert.Null(profile.Summarize());

        profile.Add(new FrameSample { FrameMilliseconds = 8, GpuMilliseconds = -1 });
        profile.Add(new FrameSample { TimeMilliseconds = 8, FrameMilliseconds = 12, GpuMilliseconds = -1 });
        ProfileSummary summary = profile.Summarize()!;

        // With less than 100 frames, the 1 % low is the slowest frame. The median of two frames is their mean.
        Assert.Equal(12, summary.OnePercentLowMilliseconds);
        Assert.Equal(10, summary.MedianMilliseconds);
        Assert.Null(summary.AverageGpuMilliseconds);
        Assert.Contains("GPU time: not known", summary.ToString(), StringComparison.Ordinal);
    }
}
