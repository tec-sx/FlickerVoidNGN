using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Ui.Tests;

/// <summary>Keeps the errors the device logs, such as validation messages, so tests can fail on them.</summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Errors
    {
        get
        {
            lock (_errors)
            {
                return [.. _errors];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose()
    {
    }

    private void Record(string message)
    {
        lock (_errors)
        {
            _errors.Add(message);
        }
    }

    private sealed class Logger(RecordingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            provider.Record(message);
        }
    }
}
