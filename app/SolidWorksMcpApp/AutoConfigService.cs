using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SolidWorksMcpApp;

/// <summary>
/// Merges the SolidWorks MCP server entry into Claude Desktop and VS Code
/// configuration files at startup, so users don't need to configure
/// anything manually after installation.
/// </summary>
internal static class AutoConfigService
{
    private static readonly JsonSerializerOptions s_writeOpts =
        new() { WriteIndented = true };

    /// <summary>
    /// Writes MCP server entries to Claude Desktop and VS Code configs.
    /// Runs silently — any failure is swallowed so it never crashes the app.
    /// </summary>
    public static void WriteConfigs()
    {
        var exePath = Environment.ProcessPath
                      ?? throw new InvalidOperationException("Process path is unavailable.");

        TryWriteClaudeConfig(exePath);
        TryWriteVsCodeConfig(exePath);
    }

    // ── Claude Desktop ────────────────────────────────────────────────────

    private static void TryWriteClaudeConfig(string exePath)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Claude");
            var file = Path.Combine(dir, "claude_desktop_config.json");

            var root = ReadOrEmpty(file);

            if (root["mcpServers"] is not JsonObject mcpServers)
            {
                mcpServers = new JsonObject();
                root["mcpServers"] = mcpServers;
            }

            mcpServers["solidworks"] = CreateClaudeServerConfig(exePath);

            Directory.CreateDirectory(dir);
            File.WriteAllText(file, root.ToJsonString(s_writeOpts));
        }
        catch
        {
            // Best-effort; ignore all errors.
        }
    }

    // ── VS Code ───────────────────────────────────────────────────────────

    private static void TryWriteVsCodeConfig(string exePath)
    {
        try
        {
            var file = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Code", "User", "mcp.json");

            var root = ReadOrEmpty(file);

            if (root["servers"] is not JsonObject servers)
            {
                servers = new JsonObject();
                root["servers"] = servers;
            }

            servers["solidworks"] = CreateVsCodeServerConfig(exePath);

            var dir = Path.GetDirectoryName(file)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(file, root.ToJsonString(s_writeOpts));
        }
        catch
        {
            // Best-effort; ignore all errors.
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static JsonObject ReadOrEmpty(string filePath)
    {
        if (!File.Exists(filePath))
            return new JsonObject();

        try
        {
            return JsonNode.Parse(File.ReadAllText(filePath)) as JsonObject
                   ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    internal static JsonObject CreateClaudeServerConfig(string exePath)
    {
        return new JsonObject
        {
            ["command"] = exePath,
            ["args"] = new JsonArray("--proxy", "--client", "Claude Desktop")
        };
    }

    internal static JsonObject CreateVsCodeServerConfig(string exePath)
    {
        return new JsonObject
        {
            ["type"] = "stdio",
            ["command"] = exePath,
            ["args"] = new JsonArray("--proxy", "--client", "VS Code")
        };
    }

    internal static string CreateOpenClawCommand(string exePath)
    {
        var payload = new JsonObject
        {
            ["command"] = exePath,
            ["args"] = new JsonArray("--proxy", "--client", "OpenClaw")
        };

        var escapedPayload = payload.ToJsonString().Replace("'", "''");
        return $"$config = '{escapedPayload}'; openclaw mcp set solidworks $config";
    }
}
