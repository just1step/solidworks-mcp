using ModelContextProtocol.Server;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;
using System.Text.Json;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public class ConnectionTools(StaDispatcher sta, ISwConnectionManager connection)
{
    [McpServerTool, Description("Connect to SolidWorks via COM. The bridge first attaches to a live SolidWorks process when one is already running; otherwise it launches the newest registered SolidWorks version. The result includes the connection path plus the 2024-only support policy and newer-version development warning.")]
    public async Task<string> SolidWorksConnect()
    {
        var result = await sta.InvokeLoggedAsync(nameof(SolidWorksConnect), null, () =>
        {
            connection.Connect();

            SolidWorksCompatibilityInfo? compatibility = null;
            CompatibilityAdvisory? compatibilityAdvisory = null;
            if (CompatibilityPolicy.TryGetCompatibilityInfo(connection, out var compatibilityInfo))
            {
                compatibility = compatibilityInfo;
                compatibilityAdvisory = CompatibilityPolicy.CreateAdvisory(compatibilityInfo);
            }

            return new
            {
                connected = connection.IsConnected,
                connectionAttempt = connection.LastConnectionAttempt,
                compatibility,
                connectionVersionCheck = compatibility?.ConnectionVersionCheck,
                compatibilityAdvisory,
            };
        });

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Disconnect from SolidWorks and release the COM connection.")]
    public async Task<string> SolidWorksDisconnect()
    {
        await sta.InvokeLoggedAsync(nameof(SolidWorksDisconnect), null, connection.Disconnect);
        return "Disconnected from SolidWorks.";
    }

    [McpServerTool, Description("Report the running SolidWorks revision, build metadata, executable path, and this bridge's compatibility classification relative to the compiled interop baseline. Use this before claiming 2025 or 2026 compatibility in a workflow.")]
    public async Task<string> GetSolidWorksCompatibility()
    {
        var result = await sta.InvokeLoggedAsync(nameof(GetSolidWorksCompatibility), null, connection.GetCompatibilityInfo);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Report the repository's explicit SolidWorks support matrix, including product-level and capability-level support states for the current target versions.")]
    public async Task<string> GetSolidWorksSupportMatrix()
    {
        var result = await sta.InvokeLoggedAsync(nameof(GetSolidWorksSupportMatrix), null, SwConnectionManager.GetCompiledSupportMatrix);
        return JsonSerializer.Serialize(result);
    }
}
