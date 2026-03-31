namespace SolidWorksMcpApp.Logging;

internal sealed record ServerLogEntry(DateTime Timestamp, string Level, string Source, string Message)
{
    public override string ToString() =>
        $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {Source}: {Message}";
}

internal static class ServerLogBuffer
{
    private const int MaxEntries = 500;
    private static readonly List<ServerLogEntry> _entries = [];
    private static readonly object _lock = new();

    public static event Action? Changed;

    public static void Append(string level, string source, string message)
    {
        lock (_lock)
        {
            _entries.Add(new ServerLogEntry(DateTime.Now, level, source, message));
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }

        Changed?.Invoke();
    }

    public static IReadOnlyList<ServerLogEntry> GetSnapshot()
    {
        lock (_lock) return _entries.ToList();
    }
}