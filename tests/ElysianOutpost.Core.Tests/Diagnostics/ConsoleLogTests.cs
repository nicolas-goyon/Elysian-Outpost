// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Diagnostics;

namespace ElysianOutpost.Core.Tests.Diagnostics;

public sealed class ConsoleLogTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_CapacityZeroOrLess_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsoleLog(capacity));
    }

    [Fact]
    public void Constructor_MaxTextLengthLessThanTwo_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsoleLog(10, 1));
    }

    [Fact]
    public void Constructor_DefaultCapacity_Is1000()
    {
        var log = new ConsoleLog();

        Assert.Equal(1000, log.Capacity);
        Assert.Equal(0, log.Count);
    }

    [Fact]
    public void Add_FullLog_ReplacesTheOldestMessage()
    {
        var log = new ConsoleLog(3);
        for (int i = 1; i <= 5; i++)
        {
            log.Add($"m{i}", ConsoleMessageLevel.Information);
        }

        Assert.Equal(3, log.Count);
        Assert.Equal(["m3", "m4", "m5"], Texts(log));
    }

    [Fact]
    public void Add_KeepsTheLevel()
    {
        var log = new ConsoleLog(4);
        log.Add("a", ConsoleMessageLevel.Warning);
        log.Add("b", ConsoleMessageLevel.Error);

        var messages = new ConsoleMessage[4];
        int count = log.CopyTo(messages);

        Assert.Equal(2, count);
        Assert.Equal(new ConsoleMessage("a", ConsoleMessageLevel.Warning), messages[0]);
        Assert.Equal(new ConsoleMessage("b", ConsoleMessageLevel.Error), messages[1]);
    }

    [Fact]
    public void Add_NullText_Throws()
    {
        var log = new ConsoleLog(4);

        Assert.Throws<ArgumentNullException>(() => log.Add(null!, ConsoleMessageLevel.Information));
    }

    [Fact]
    public void Add_LongText_KeepsTheStartAndAnEllipsis()
    {
        var log = new ConsoleLog(4, maxTextLength: 5);
        log.Add("abcde", ConsoleMessageLevel.Information);
        log.Add("abcdefgh", ConsoleMessageLevel.Information);

        Assert.Equal(["abcde", "abcd…"], Texts(log));
    }

    // "\U0001F600" is one character in two UTF-16 code units (a surrogate pair). The cut must not separate them.
    [Fact]
    public void Add_LongTextWithASurrogatePairAtTheCut_KeepsThePairComplete()
    {
        var log = new ConsoleLog(4, maxTextLength: 5);
        log.Add("abc\U0001F600xyz", ConsoleMessageLevel.Information);

        Assert.Equal(["abc…"], Texts(log));
    }

    [Fact]
    public void Clear_RemovesAllTheMessages()
    {
        var log = new ConsoleLog(4);
        log.Add("a", ConsoleMessageLevel.Information);
        log.Add("b", ConsoleMessageLevel.Information);

        log.Clear();
        log.Add("c", ConsoleMessageLevel.Information);

        Assert.Equal(1, log.Count);
        Assert.Equal(["c"], Texts(log));
    }

    [Fact]
    public void CopyTo_ShortSpan_GivesTheNewestMessages()
    {
        var log = new ConsoleLog(4);
        log.Add("a", ConsoleMessageLevel.Information);
        log.Add("b", ConsoleMessageLevel.Information);
        log.Add("c", ConsoleMessageLevel.Information);

        var messages = new ConsoleMessage[2];
        int count = log.CopyTo(messages);

        Assert.Equal(2, count);
        Assert.Equal(["b", "c"], messages.Select(message => message.Text));
    }

    [Fact]
    public void GetText_GivesTheTextsWithALineFeedBetweenThem()
    {
        var log = new ConsoleLog(2);
        Assert.Equal(string.Empty, log.GetText());

        log.Add("a", ConsoleMessageLevel.Information);
        log.Add("b\nc", ConsoleMessageLevel.Information);
        log.Add("d", ConsoleMessageLevel.Information);

        Assert.Equal("b\nc\nd", log.GetText());
    }

    private static string[] Texts(ConsoleLog log)
    {
        var messages = new ConsoleMessage[log.Capacity];
        int count = log.CopyTo(messages);
        return messages.Take(count).Select(message => message.Text).ToArray();
    }
}
