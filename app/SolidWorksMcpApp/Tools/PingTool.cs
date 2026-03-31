using ModelContextProtocol.Server;
using System.ComponentModel;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public static class PingTool
{
    [McpServerTool, Description("Ping the SolidWorks MCP server to verify connectivity.")]
    public static string Ping() => "pong — SolidWorks MCP server is running";
}
