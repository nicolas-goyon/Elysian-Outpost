// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.Core.Tests.Diagnostics;

public sealed class ConsoleLogReaderTests
{
    [Fact]
    public void Constructor_NullLog_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ConsoleLogReader(null!));
    }

    [Fact]
    public void ReadChanges_NoNewMessage_GivesNoChange()
    {
        var log = new ConsoleLog(4);
        var display = new Display(log);

        Assert.False(display.Update());
        Assert.Empty(display.Texts);
    }

    [Fact]
    public void ReadChanges_NewMessages_GivesOnlyTheNewMessages()
    {
        var log = new ConsoleLog(4);
        var display = new Display(log);
        log.Add("a", ConsoleMessageLevel.Information);
        log.Add("b", ConsoleMessageLevel.Information);
        display.Update();

        log.Add("c", ConsoleMessageLevel.Information);
        ConsoleLogChanges changes = display.Reader.ReadChanges();

        Assert.False(changes.Restart);
        Assert.Equal(1, changes.Messages.Length);
        Assert.Equal("c", changes.Messages[0].Text);
        Assert.Equal(3, display.Reader.DisplayedCount);
    }

    [Fact]
    public void ReadChanges_FullLog_RestartsWithTheLastMessages()
    {
        var log = new ConsoleLog(3);
        var display = new Display(log);
        log.Add("a", ConsoleMessageLevel.Information);
        log.Add("b", ConsoleMessageLevel.Information);
        display.Update();

        log.Add("c", ConsoleMessageLevel.Information);
        log.Add("d", ConsoleMessageLevel.Information);
        ConsoleLogChanges changes = display.Reader.ReadChanges();

        Assert.True(changes.Restart);
        Assert.Equal(["b", "c", "d"], changes.Messages.ToArray().Select(message => message.Text));
        Assert.Equal(3, display.Reader.DisplayedCount);
    }

    [Fact]
    public void ReadChanges_FullLogAndEmptyDisplay_DoesNotRestart()
    {
        var log = new ConsoleLog(3);
        var display = new Display(log);
        for (int i = 1; i <= 5; i++)
        {
            log.Add($"m{i}", ConsoleMessageLevel.Information);
        }

        ConsoleLogChanges changes = display.Reader.ReadChanges();

        Assert.False(changes.Restart);
        Assert.Equal(["m3", "m4", "m5"], changes.Messages.ToArray().Select(message => message.Text));
    }

    [Fact]
    public void ReadChanges_AfterClear_RestartsWithNoMessage()
    {
        var log = new ConsoleLog(4);
        var display = new Display(log);
        log.Add("a", ConsoleMessageLevel.Information);
        display.Update();

        log.Clear();
        ConsoleLogChanges changes = display.Reader.ReadChanges();

        Assert.True(changes.Restart);
        Assert.Equal(0, changes.Messages.Length);
        Assert.Equal(0, display.Reader.DisplayedCount);
    }

    [Fact]
    public void ReadChanges_ClearAndNewMessages_RestartsWithTheNewMessages()
    {
        var log = new ConsoleLog(4);
        var display = new Display(log);
        log.Add("a", ConsoleMessageLevel.Information);
        display.Update();

        log.Clear();
        log.Add("b", ConsoleMessageLevel.Information);
        display.Update();

        Assert.Equal(["b"], display.Texts);
    }

    // The test of issue #37: four threads add 10,000 messages, and the main thread shows them at the same time.
    // The display never has more than 1,000 messages. At the end, it has the last 1,000 messages of the log.
    [Fact]
    public void ReadChanges_FourThreadsAdd10000Messages_DisplayHasTheLast1000Messages()
    {
        const int ThreadCount = 4;
        const int MessagesPerThread = 2_500;
        var log = new ConsoleLog();
        var display = new Display(log);
        using var start = new Barrier(ThreadCount + 1);
        var writers = new Thread[ThreadCount];
        for (int t = 0; t < ThreadCount; t++)
        {
            int thread = t;
            writers[t] = new Thread(() =>
            {
                start.SignalAndWait();
                for (int i = 0; i < MessagesPerThread; i++)
                {
                    log.Add($"{thread}:{i}", (ConsoleMessageLevel)(i % 6));
                }
            });
            writers[t].Start();
        }

        start.SignalAndWait();
        int maxDisplayed = 0;
        while (writers.Any(writer => writer.IsAlive))
        {
            display.Update();
            maxDisplayed = Math.Max(maxDisplayed, display.Texts.Count);
        }

        foreach (Thread writer in writers)
        {
            writer.Join();
        }

        display.Update();

        Assert.True(maxDisplayed <= 1_000, $"The display had {maxDisplayed} messages.");
        Assert.Equal(1_000, display.Texts.Count);
        Assert.Equal(1_000, display.Reader.DisplayedCount);

        // The display has the same messages as the log, in the same order.
        var expected = new ConsoleMessage[1_000];
        Assert.Equal(1_000, log.CopyTo(expected));
        Assert.Equal(expected.Select(message => message.Text), display.Texts);

        // The log keeps the newest messages. Thus the messages of each thread are the last messages of the thread, in order.
        for (int t = 0; t < ThreadCount; t++)
        {
            int[] numbers = display.Texts
                .Where(text => text.StartsWith($"{t}:", StringComparison.Ordinal))
                .Select(text => int.Parse(text.AsSpan(text.IndexOf(':') + 1), provider: null))
                .ToArray();
            int firstNumber = MessagesPerThread - numbers.Length;
            Assert.Equal(Enumerable.Range(firstNumber, numbers.Length), numbers);
        }
    }

    // A display that applies the changes of a reader, as the console window of the app does.
    private sealed class Display(ConsoleLog log)
    {
        public ConsoleLogReader Reader { get; } = new(log);

        public List<string> Texts { get; } = [];

        // Applies the changes. Returns true if the display changed.
        public bool Update()
        {
            ConsoleLogChanges changes = Reader.ReadChanges();
            if (changes.Restart)
            {
                Texts.Clear();
            }

            foreach (ConsoleMessage message in changes.Messages)
            {
                Texts.Add(message.Text);
            }

            return changes.Restart || changes.Messages.Length > 0;
        }
    }
}
