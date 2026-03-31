using SolidWorksMcpApp.Logging;

namespace SolidWorksMcpApp.Ipc;

/// <summary>
/// Thread-safe registry of currently connected Proxy sessions.
/// Fires <see cref="Changed"/> on the calling thread whenever the list changes.
/// </summary>
internal static class ClientRegistry
{
    private static readonly List<ClientInfo> _clients = [];
    private static readonly object _lock = new();

    /// <summary>Raised (on whichever thread modified the registry) after every Add or Remove.</summary>
    public static event Action? Changed;

    public static void Add(ClientInfo c)
    {
        lock (_lock) _clients.Add(c);
        ServerLogBuffer.Append("INFO", "Hub", Strings.LogClientConnected(c.Name, c.Pid));
        Changed?.Invoke();
    }

    public static void Remove(string sessionId)
    {
        ClientInfo? removed = null;
        lock (_lock)
        {
            removed = _clients.FirstOrDefault(c => c.SessionId == sessionId);
            _clients.RemoveAll(c => c.SessionId == sessionId);
        }

        if (removed is not null)
            ServerLogBuffer.Append("INFO", "Hub", Strings.LogClientDisconnected(removed.Name, removed.Pid));

        Changed?.Invoke();
    }

    public static IReadOnlyList<ClientInfo> GetSnapshot()
    {
        lock (_lock) return _clients.ToList();
    }

    public static int Count
    {
        get { lock (_lock) return _clients.Count; }
    }
}
