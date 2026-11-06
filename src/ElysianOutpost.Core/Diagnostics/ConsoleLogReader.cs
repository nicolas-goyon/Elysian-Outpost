// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// Gives the changes of a <see cref="ConsoleLog"/> to the display of the messages, for example a console window.
/// </summary>
/// <remarks>
/// <para>
/// The reader knows the messages that the display shows. In each frame, the display calls <see cref="ReadChanges"/> and applies the changes.
/// Then the display shows the same messages as the log, and never more than <see cref="ConsoleLog.Capacity"/> messages.
/// </para>
/// <para>
/// Only one thread can use a reader, usually the main thread. The threads that add messages to the log do not use the reader.
/// The reader does not allocate memory after the constructor.
/// </para>
/// </remarks>
public sealed class ConsoleLogReader
{
    private readonly ConsoleLog _log;
    private readonly ConsoleMessage[] _messages;

    // The sequence number of the first message that the display shows, and the sequence number of the next message to read.
    private long _first;
    private long _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogReader"/> class. The display of a new reader is empty.
    /// </summary>
    /// <param name="log">The log to read.</param>
    /// <exception cref="ArgumentNullException">The log is <c>null</c>.</exception>
    public ConsoleLogReader(ConsoleLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
        _messages = new ConsoleMessage[log.Capacity];
    }

    /// <summary>
    /// Gets the number of messages that the display shows after the last changes.
    /// </summary>
    public int DisplayedCount => (int)(_next - _first);

    /// <summary>
    /// Reads the changes of the log since the last call.
    /// </summary>
    /// <returns>The changes. Do not keep them after the next call.</returns>
    public ConsoleLogChanges ReadChanges()
    {
        int count = _log.Read(ref _first, ref _next, _messages, out bool restart);
        return new ConsoleLogChanges(restart, _messages.AsSpan(0, count));
    }
}
