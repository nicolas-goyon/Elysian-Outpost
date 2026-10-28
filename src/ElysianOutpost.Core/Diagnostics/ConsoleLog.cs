// SPDX-License-Identifier: MPL-2.0

using System.Text;

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// Keeps the last messages of the in-game console. All threads can add messages at the same time.
/// </summary>
/// <remarks>
/// <para>
/// The log has a fixed capacity. When the log is full, a new message replaces the oldest message.
/// A <see cref="ConsoleLogReader"/> gives the changes of the log to the thread that shows the messages.
/// </para>
/// <para>
/// Each message gets a sequence number. The numbers start at 0 and increase by 1 for each message.
/// The log keeps the messages from the number <c>_oldest</c> to the number <c>_next</c> - 1.
/// </para>
/// <para>
/// A lock protects all the fields. Each operation keeps the lock for a short time.
/// <see cref="Add"/> allocates memory only to make a long text shorter, and never while it keeps the lock.
/// Thus a thread that writes many messages does not slow the other threads much.
/// </para>
/// </remarks>
public sealed class ConsoleLog
{
    /// <summary>
    /// The default maximum number of messages, from issue #37.
    /// </summary>
    public const int DefaultCapacity = 1000;

    // The end of a text that the log made shorter.
    private const string Ellipsis = "…";

    private readonly Lock _lock = new();
    private readonly ConsoleMessage[] _messages;

    // The sequence number of the oldest message in the log, and the sequence number of the next message.
    private long _oldest;
    private long _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLog"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of messages.</param>
    /// <param name="maxTextLength">The maximum number of characters in the text of a message. The log makes a longer text shorter.</param>
    /// <exception cref="ArgumentOutOfRangeException">The capacity is zero or less, or the maximum length is less than 2.</exception>
    public ConsoleLog(int capacity = DefaultCapacity, int maxTextLength = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTextLength, 2);
        Capacity = capacity;
        MaxTextLength = maxTextLength;
        _messages = new ConsoleMessage[capacity];
    }

    /// <summary>
    /// Gets the maximum number of messages.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the maximum number of characters in the text of a message.
    /// </summary>
    public int MaxTextLength { get; }

    /// <summary>
    /// Gets the number of messages in the log.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return (int)(_next - _oldest);
            }
        }
    }

    /// <summary>
    /// Adds a message. If the log is full, the message replaces the oldest message. All threads can call this method.
    /// </summary>
    /// <param name="text">The text of the message. If the text is longer than <see cref="MaxTextLength"/>, the log keeps only its start.</param>
    /// <param name="level">The importance of the message.</param>
    /// <exception cref="ArgumentNullException">The text is <c>null</c>.</exception>
    public void Add(string text, ConsoleMessageLevel level)
    {
        ArgumentNullException.ThrowIfNull(text);
        var message = new ConsoleMessage(Shorten(text), level);
        lock (_lock)
        {
            _messages[_next % Capacity] = message;
            _next++;
            if (_next - _oldest > Capacity)
            {
                _oldest++;
            }
        }
    }

    /// <summary>
    /// Removes all the messages. All threads can call this method.
    /// </summary>
    /// <remarks>
    /// The sequence numbers continue after the removal. Thus a reader finds the removal and removes the messages that it shows.
    /// </remarks>
    public void Clear()
    {
        lock (_lock)
        {
            // The log must not keep the texts. Thus the garbage collector can release them.
            Array.Clear(_messages);
            _oldest = _next;
        }
    }

    /// <summary>
    /// Copies the messages to a span, from the oldest message to the newest message. All threads can call this method.
    /// </summary>
    /// <param name="destination">The span that receives the messages. If it is too short, it receives only the newest messages.</param>
    /// <returns>The number of messages in the span.</returns>
    public int CopyTo(Span<ConsoleMessage> destination)
    {
        lock (_lock)
        {
            long first = Math.Max(_oldest, _next - destination.Length);
            return CopyRange(first, destination);
        }
    }

    /// <summary>
    /// Gets the texts of all the messages, from the oldest message to the newest message, with a line feed between two texts.
    /// All threads can call this method.
    /// </summary>
    /// <returns>The texts of the messages, or an empty string if the log is empty.</returns>
    public string GetText()
    {
        var builder = new StringBuilder();
        lock (_lock)
        {
            for (long sequence = _oldest; sequence < _next; sequence++)
            {
                if (sequence > _oldest)
                {
                    builder.Append('\n');
                }

                builder.Append(_messages[sequence % Capacity].Text);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the changes since the last read of a reader, and updates the position of the reader.
    /// </summary>
    /// <param name="first">The sequence number of the first message that the reader shows.</param>
    /// <param name="next">The sequence number of the next message for the reader.</param>
    /// <param name="destination">The span that receives the messages. Its length must be equal to or more than <see cref="Capacity"/>.</param>
    /// <param name="restart">
    /// <c>true</c> if the reader must remove all the messages that it shows before it shows the messages of the span.
    /// </param>
    /// <returns>The number of messages in the span.</returns>
    internal int Read(ref long first, ref long next, Span<ConsoleMessage> destination, out bool restart)
    {
        lock (_lock)
        {
            long from = next;
            restart = false;

            // The log removed messages that the reader did not receive or that the reader shows.
            // Then the reader shows only the messages that the log keeps.
            if (_oldest > first)
            {
                restart = next > first;
                first = _oldest;
                from = _oldest;
            }

            int count = CopyRange(from, destination);
            next = _next;
            return count;
        }
    }

    // Copies the messages from a sequence number to the newest message. The caller keeps the lock.
    private int CopyRange(long from, Span<ConsoleMessage> destination)
    {
        int count = (int)(_next - from);
        for (int i = 0; i < count; i++)
        {
            destination[i] = _messages[(from + i) % Capacity];
        }

        return count;
    }

    private string Shorten(string text)
    {
        if (text.Length <= MaxTextLength)
        {
            return text;
        }

        // The ellipsis is one of the characters. A cut must not separate the two halves of a surrogate pair.
        int length = MaxTextLength - 1;
        if (char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return string.Concat(text.AsSpan(0, length), Ellipsis);
    }
}
