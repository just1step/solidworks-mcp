namespace SolidWorksMcpApp.Ipc;

/// <summary>Represents one connected Proxy client session.</summary>
internal sealed class ClientInfo
{
    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..8];
    public string Name      { get; set; } = "Unknown";
    public int    Pid       { get; set; }
    public DateTime ConnectedAt { get; } = DateTime.Now;
}
