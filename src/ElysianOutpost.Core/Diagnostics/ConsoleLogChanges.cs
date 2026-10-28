// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// The changes of a <see cref="ConsoleLog"/> for a display.
/// </summary>
/// <param name="restart">
/// <c>true</c> if the display must remove all its messages before it adds <paramref name="messages"/>.
/// This occurs after <see cref="ConsoleLog.Clear"/>, or when the log removed a message that the display shows.
/// </param>
/// <param name="messages">The messages that the display must add, from the oldest message to the newest message.</param>
public readonly ref struct ConsoleLogChanges(bool restart, ReadOnlySpan<ConsoleMessage> messages)
{
    /// <summary>
    /// Gets a value that tells if the display must remove all its messages before it adds <see cref="Messages"/>.
    /// </summary>
    public bool Restart { get; } = restart;

    /// <summary>
    /// Gets the messages that the display must add, from the oldest message to the newest message.
    /// </summary>
    public ReadOnlySpan<ConsoleMessage> Messages { get; } = messages;
}
