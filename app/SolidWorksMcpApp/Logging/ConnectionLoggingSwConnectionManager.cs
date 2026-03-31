using SolidWorksBridge.SolidWorks;
using System.Text.Json;

namespace SolidWorksMcpApp.Logging;

internal sealed class ConnectionLoggingSwConnectionManager(
    ISwConnectionManager inner,
    Func<ISelectionService> selectionServiceFactory) : ISwConnectionManager
{
    private static readonly object s_fileLock = new();

    public bool IsConnected => inner.IsConnected;

    public ISldWorksApp? SwApp => inner.SwApp;

    public void Connect()
    {
        bool wasConnected = inner.IsConnected;
        inner.Connect();

        if (!wasConnected && inner.IsConnected)
        {
            CaptureAndLogContext();
        }
    }

    public void Disconnect() => inner.Disconnect();

    public void EnsureConnected() => inner.EnsureConnected();

    private void CaptureAndLogContext()
    {
        try
        {
            var context = selectionServiceFactory().GetSolidWorksContext();
            string payload = JsonSerializer.Serialize(context);
            Append("INFO", "SolidWorks", $"SolidWorks context after connect: {payload}");
        }
        catch (Exception ex)
        {
            Append("WARN", "SolidWorks", $"Connected to SolidWorks, but failed to capture context: {ex.Message}");
        }
    }

    private static void Append(string level, string source, string message)
    {
        ServerLogBuffer.Append(level, source, message);

        if (string.IsNullOrWhiteSpace(ServerState.LogFilePath))
        {
            return;
        }

        var client = ServerState.ConnectedClientName is { } name ? $" [{name}]" : string.Empty;
        var line = $"[{level}]{client} {DateTime.Now:yyyy-MM-dd HH:mm:ss} {source}: {message}";

        try
        {
            lock (s_fileLock)
            {
                File.AppendAllText(ServerState.LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Never fail the connection because logging could not be persisted.
        }
    }
}