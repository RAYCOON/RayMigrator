using Microsoft.Extensions.Logging;

namespace Raycoon.RayMigrator.Tests.Unit.Helpers;

/// <summary>
/// A logger that captures log entries for assertion in unit tests.
/// </summary>
internal class CapturingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry
        {
            LogLevel = logLevel,
            Message = formatter(state, exception),
            Exception = exception
        });
    }
}

internal class LogEntry
{
    public LogLevel LogLevel { get; init; }
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
}
