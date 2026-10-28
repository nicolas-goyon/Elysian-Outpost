// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Diagnostics;

/// <summary>
/// The importance of a message of the in-game console. The values have the order and the names of the log levels.
/// </summary>
public enum ConsoleMessageLevel
{
    /// <summary>
    /// A message with the most details, for the analysis of a problem.
    /// </summary>
    Trace,

    /// <summary>
    /// A message for the development of the app.
    /// </summary>
    Debug,

    /// <summary>
    /// A message about the usual operation of the app, for example the output of a console command.
    /// </summary>
    Information,

    /// <summary>
    /// A message about an unusual event. The app continues.
    /// </summary>
    Warning,

    /// <summary>
    /// A message about a failure of an operation.
    /// </summary>
    Error,

    /// <summary>
    /// A message about a failure that can stop the app.
    /// </summary>
    Critical,
}
