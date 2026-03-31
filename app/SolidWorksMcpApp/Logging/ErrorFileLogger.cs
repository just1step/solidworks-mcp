using Microsoft.Extensions.Logging;

namespace SolidWorksMcpApp.Logging;

/// <summary>Appends error-level log entries to a file. Thread-safe.</summary>
internal sealed class ErrorFileLogger(string category, string filePath) : ILogger
{
    private static readonly object s_lock = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel level) => level >= LogLevel.Error;

    public void Log<TState>(
        LogLevel level,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level)) return;

        var client = ServerState.ConnectedClientName is { } name ? $" [{name}]" : "";
        var line = $"[{level.ToString().ToUpper()}]{client} {DateTime.Now:yyyy-MM-dd HH:mm:ss} {category}: {formatter(state, exception)}";
        if (exception is not null)
            line += $"\n  {exception.GetType().Name}: {exception.Message}";

        ServerLogBuffer.Append(level.ToString().ToUpper(), category, formatter(state, exception));

        try
        {
            lock (s_lock)
                File.AppendAllText(filePath, line + "\n");
        }
        catch { /* never crash the host over a log write */ }
    }
}
