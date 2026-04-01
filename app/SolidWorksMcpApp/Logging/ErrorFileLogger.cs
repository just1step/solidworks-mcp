using Microsoft.Extensions.Logging;

namespace SolidWorksMcpApp.Logging;

/// <summary>Forwards ILogger entries into the in-memory monitor and the session log file.</summary>
internal sealed class ErrorFileLogger(string category, string filePath) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel level) => level != LogLevel.None;

    public void Log<TState>(
        LogLevel level,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level)) return;

        _ = filePath;
        ServerLogBuffer.Append(level.ToString().ToUpperInvariant(), category, formatter(state, exception), exception);
    }
}
