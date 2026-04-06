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
                CaptureAndLogConnectionSnapshot();
                return;
            }

            if (wasConnected && inner.IsConnected)
            {
                ServerLogBuffer.Append("INFO", "COM", "Connect refreshed the SolidWorks session.");
                CaptureAndLogConnectionSnapshot();
                return;
            }

            if (inner.IsConnected)
            {
                ServerLogBuffer.Append("INFO", "COM", "Connected to SolidWorks.");
                CaptureAndLogConnectionSnapshot();
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
            var previousApp = inner.SwApp;
            bool wasConnected = inner.IsConnected;
            inner.EnsureConnected();

            var currentApp = inner.SwApp;
            if (!inner.IsConnected)
            {
                return;
            }

            if (!wasConnected)
            {
                ServerLogBuffer.Append("INFO", "COM", "EnsureConnected established the SolidWorks session.");
                CaptureAndLogConnectionSnapshot();
                return;
            }

            if (!ReferenceEquals(previousApp, currentApp))
            {
                ServerLogBuffer.Append("INFO", "COM", "EnsureConnected refreshed the SolidWorks session.");
                CaptureAndLogConnectionSnapshot();
            }
        }
        catch (Exception ex)
        {
            ServerLogBuffer.Append("ERROR", "COM", "EnsureConnected failed.", ex);
            throw;
        }
    }

    public SolidWorksCompatibilityInfo GetCompatibilityInfo()
    {
        try
        {
            return inner.GetCompatibilityInfo();
        }
        catch (Exception ex)
        {
            ServerLogBuffer.Append("ERROR", "COM", "Compatibility detection failed.", ex);
            throw;
        }
    }

    private void CaptureAndLogConnectionSnapshot()
    {
        try
        {
            var compatibility = inner.GetCompatibilityInfo();
            string compatibilityPayload = JsonSerializer.Serialize(compatibility);
            ServerLogBuffer.Append("INFO", "COM", $"SolidWorks connection after connect: {compatibilityPayload}");

            var context = selectionServiceFactory().GetSolidWorksContext();
            string payload = JsonSerializer.Serialize(context);
            ServerLogBuffer.Append("INFO", "COM", $"SolidWorks context after connect: {payload}");
        }
        catch (Exception ex)
        {
            ServerLogBuffer.Append("WARN", "COM", "Connected to SolidWorks, but failed to capture connection details.", ex);
        }
    }
}