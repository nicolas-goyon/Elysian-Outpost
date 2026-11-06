// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// One message of the in-game console.
/// </summary>
/// <param name="Text">The text of the message. It can have more than one line.</param>
/// <param name="Level">The importance of the message.</param>
public readonly record struct ConsoleMessage(string Text, ConsoleMessageLevel Level);
