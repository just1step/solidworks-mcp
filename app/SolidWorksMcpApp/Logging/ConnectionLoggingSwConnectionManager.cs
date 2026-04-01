using SolidWorksBridge.SolidWorks;
using System.Text.Json;

namespace SolidWorksMcpApp.Logging;

internal sealed class ConnectionLoggingSwConnectionManager(
    ISwConnectionManager inner,
    Func<ISelectionService> selectionServiceFactory) : ISwConnectionManager
{
    public bool IsConnected => inner.IsConnected;

    public ISldWorksApp? SwApp => inner.SwApp;

    public void Connect()
    {
        ServerLogBuffer.Append("INFO", "COM", "Connect requested.");
        try
        {
            var previousApp = inner.SwApp;
            bool wasConnected = inner.IsConnected;
            inner.Connect();
            var currentApp = inner.SwApp;

            if (wasConnected && ReferenceEquals(previousApp, currentApp))
            {
                ServerLogBuffer.Append("INFO", "COM", "Connect reused the existing SolidWorks session.");
                return;
            }

            if (wasConnected && inner.IsConnected)
            {
                ServerLogBuffer.Append("INFO", "COM", "Connect refreshed the SolidWorks session.");
                CaptureAndLogContext();
                return;
            }

            if (inner.IsConnected)
            {
                ServerLogBuffer.Append("INFO", "COM", "Connected to SolidWorks.");
                CaptureAndLogContext();
            }
        }
        catch (Exception ex)
        {
            ServerLogBuffer.Append("ERROR", "COM", "Connect failed.", ex);
            throw;
        }
    }

    public void Disconnect()
    {
        ServerLogBuffer.Append("INFO", "COM", "Disconnect requested.");
        try
        {
            inner.Disconnect();
            ServerLogBuffer.Append("INFO", "COM", "Disconnected from SolidWorks.");
        }
        catch (Exception ex)
        {
            ServerLogBuffer.Append("ERROR", "COM", "Disconnect failed.", ex);
            throw;
        }
    }

    public void EnsureConnected()
    {
        try
        {
            inner.EnsureConnected();
        }
        catch (Exception ex)
        {
            ServerLogBuffer.Append("ERROR", "COM", "EnsureConnected failed.", ex);
            throw;
        }
    }

    private void CaptureAndLogContext()
    {
        try
        {
            var context = selectionServiceFactory().GetSolidWorksContext();
            string payload = JsonSerializer.Serialize(context);
            ServerLogBuffer.Append("INFO", "COM", $"SolidWorks context after connect: {payload}");
        }
        catch (Exception ex)
        {
            ServerLogBuffer.Append("WARN", "COM", "Connected to SolidWorks, but failed to capture context.", ex);
        }
    }
}