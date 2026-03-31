namespace SolidWorksMcpApp;

internal static class ServerState
{
    /// <summary>When true, InvokeAsync on the STA dispatcher rejects all calls immediately.</summary>
    public static volatile bool IsPaused = false;

    /// <summary>MCP client name captured on first tool call (one client per stdio process).</summary>
    public static string? ConnectedClientName = null;

    /// <summary>Absolute path of the error log file for this session.</summary>
    public static string LogFilePath { get; private set; } = string.Empty;

    /// <summary>
    /// Creates logs/{MachineName}_{yyyyMMdd_HHmmss}.txt next to the exe
    /// and stores the path in <see cref="LogFilePath"/>.
    /// </summary>
    public static void InitLogFile()
    {
        var exeDir = AppContext.BaseDirectory;

        var logsDir = Path.Combine(exeDir, "logs");
        Directory.CreateDirectory(logsDir);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        LogFilePath = Path.Combine(logsDir, $"{Environment.MachineName}_{stamp}.txt");
    }
}
