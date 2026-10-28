// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ElysianOutpost.App.Logging;

/// <summary>
/// Sends each log message to the log of the in-game console (#37).
/// </summary>
/// <remarks>
/// All threads can log messages, for example the threads that build the chunks. The provider only adds the messages to a
/// <see cref="ConsoleLog"/>, which is safe for all threads. The main thread then shows them in the console window.
/// The text of a message has the time, the level and the category, as a line of the log file.
/// The console window adds its own time to each line: the time when the line arrived in the window.
/// </remarks>
/// <param name="log">The log of the in-game console.</param>
internal sealed class ConsoleLogLoggerProvider(ConsoleLog log) : ILoggerProvider
{
    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new ConsoleLogLogger(log, categoryName);

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private static ConsoleMessageLevel ToMessageLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => ConsoleMessageLevel.Trace,
        LogLevel.Debug => ConsoleMessageLevel.Debug,
        LogLevel.Warning => ConsoleMessageLevel.Warning,
        LogLevel.Error => ConsoleMessageLevel.Error,
        LogLevel.Critical => ConsoleMessageLevel.Critical,
        _ => ConsoleMessageLevel.Information,
    };

    private sealed class ConsoleLogLogger(ConsoleLog log, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string text = $"{DateTimeOffset.Now:HH:mm:ss.fff} [{TextWriterLoggerProvider.LevelName(logLevel)}] {categoryName}: {formatter(state, exception)}";
            if (exception is not null)
            {
                text = $"{text}\n{exception}";
            }

            log.Add(text, ToMessageLevel(logLevel));
        }
    }
}
