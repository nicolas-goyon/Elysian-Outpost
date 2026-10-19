// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;

namespace ElysianOutpost.App.Logging;

/// <summary>
/// Writes each log message as one line of text, for example to the console or to a file.
/// </summary>
/// <remarks>
/// The provider writes each line immediately. Thus the app does not lose the last messages when it stops.
/// The logging system of BRUTAL does not dispose the providers. Thus the app must dispose this provider before it stops.
/// If the writer fails, for example because the disk is full, the provider stops and ignores the next messages.
/// Thus a problem of the log file does not stop the app.
/// </remarks>
/// <param name="writer">The destination of the lines.</param>
/// <param name="ownsWriter"><c>true</c> to dispose the writer with the provider.</param>
internal sealed class TextWriterLoggerProvider(TextWriter writer, bool ownsWriter) : ILoggerProvider
{
    private readonly Lock _lock = new();
    private bool _disposed;
    private bool _failed;

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new TextWriterLogger(this, categoryName);

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                if (!_failed)
                {
                    writer.Flush();
                }
            }
            catch (IOException)
            {
                _failed = true;
            }
            finally
            {
                if (ownsWriter)
                {
                    DisposeWriter();
                }
            }
        }
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRITICAL",
        _ => level.ToString(),
    };

    private void Write(string categoryName, LogLevel level, string message, Exception? exception)
    {
        string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{LevelName(level)}] {categoryName}: {message}";
        lock (_lock)
        {
            // A message after the disposal or after a failure has no destination. The provider ignores it.
            if (_disposed || _failed)
            {
                return;
            }

            try
            {
                writer.WriteLine(line);
                if (exception is not null)
                {
                    writer.WriteLine(exception);
                }

                writer.Flush();
            }
            catch (IOException failure)
            {
                _failed = true;
                ReportFailure(failure);
            }
        }
    }

    // The provider cannot use the logging system to report its own failure. Thus it writes one line to the error output.
    private static void ReportFailure(IOException failure)
    {
        try
        {
            Console.Error.WriteLine($"A log writer failed and stops: {failure.Message}");
        }
        catch (IOException)
        {
            // The error output also failed. No other destination is available.
        }
    }

    // The disposal of a writer can also write the buffered text. Thus it can fail like a write.
    private void DisposeWriter()
    {
        try
        {
            writer.Dispose();
        }
        catch (IOException)
        {
            _failed = true;
        }
    }

    private sealed class TextWriterLogger(TextWriterLoggerProvider provider, string categoryName) : ILogger
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
            if (IsEnabled(logLevel))
            {
                provider.Write(categoryName, logLevel, formatter(state, exception), exception);
            }
        }
    }
}
