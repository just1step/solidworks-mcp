using ModelContextProtocol.Server;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public class ConnectionTools(StaDispatcher sta, ISwConnectionManager connection)
{
    [McpServerTool, Description("Connect to the currently running SolidWorks instance via COM.")]
    public async Task<string> SolidWorksConnect()
    {
        await sta.InvokeAsync(connection.Connect);
        return "Connected to SolidWorks.";
    }

    [McpServerTool, Description("Disconnect from SolidWorks and release the COM connection.")]
    public async Task<string> SolidWorksDisconnect()
    {
        await sta.InvokeAsync(connection.Disconnect);
        return "Disconnected from SolidWorks.";
    }
}
