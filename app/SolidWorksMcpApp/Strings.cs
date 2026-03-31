using System.Globalization;

namespace SolidWorksMcpApp;

/// <summary>
/// UI strings that follow the Windows display language.
/// Falls back to English when the current culture is not Chinese.
/// </summary>
internal static class Strings
{
    internal static bool IsChinese =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";

    public static string AppTitle    => "SolidWorks MCP Server";

    public static string AlreadyRunning =>
        IsChinese ? "SolidWorks MCP Server 已在运行中。"
                  : "SolidWorks MCP Server is already running.";

    // ── Tray menu ─────────────────────────────────────────────────────────

    public static string StatusRunning  => IsChinese ? "状态: 运行中"  : "Status: Running";
    public static string StatusPaused   => IsChinese ? "状态: 已暂停"  : "Status: Paused";
    public static string MenuStart      => IsChinese ? "启动"          : "Start";
    public static string MenuPause      => IsChinese ? "暂停"          : "Pause";
    public static string MenuMonitor    => IsChinese ? "打开监控面板"   : "Open Monitor";
    public static string MenuVsCode     => IsChinese ? "导出 VS Code MCP 配置"  : "Export VS Code MCP Config";
    public static string MenuClaude     => IsChinese ? "导出 Claude 配置"  : "Export Claude Config";
    public static string MenuOpenLog    => IsChinese ? "查看出错日志"       : "View Error Log";
    public static string MenuExit       => IsChinese ? "退出"             : "Exit";

    public static string MonitorTitle         => IsChinese ? "SolidWorks MCP 监控面板" : "SolidWorks MCP Monitor";
    public static string MonitorClientsHeading => IsChinese ? "当前连接" : "Connected Clients";
    public static string MonitorLogsHeading    => IsChinese ? "最近日志" : "Recent Logs";
    public static string MonitorColClient      => IsChinese ? "客户端" : "Client";
    public static string MonitorColPid         => IsChinese ? "进程 ID" : "PID";
    public static string MonitorColConnected   => IsChinese ? "连接时间" : "Connected At";
    public static string MonitorColSession     => IsChinese ? "会话" : "Session";

    // ── Balloon tips ──────────────────────────────────────────────────────

    public static string BalloonStarted =>
        IsChinese ? "服务已恢复，可接受新请求。"
                  : "Server resumed. Ready to accept requests.";

    public static string BalloonPaused =>
        IsChinese ? "服务已暂停，工具调用将返回错误直到恢复。"
                  : "Server paused. Tool calls will return errors until resumed.";

    public static string BalloonCopied  => IsChinese ? "已复制"  : "Copied";
    public static string BalloonNoLog   => IsChinese ? "无日志"  : "No Log";
    public static string BalloonClipboardBusy => IsChinese ? "剪贴板被占用" : "Clipboard Busy";

    public static string BalloonClaudeCopied =>
        IsChinese ? "Claude Desktop 配置已复制到剪贴板。"
                  : "Claude Desktop config copied to clipboard.";

    public static string BalloonVsCodeCopied =>
        IsChinese ? "VS Code mcp.json 配置已复制到剪贴板。"
                  : "VS Code mcp.json config copied to clipboard.";

    public static string BalloonClipboardFallback =>
        IsChinese ? "剪贴板暂时不可用，已打开临时配置文件供手动复制。"
                  : "Clipboard is temporarily unavailable. Opened a temporary config file for manual copy.";

    public static string BalloonNoLogBody =>
        IsChinese ? "本次会话暂无错误日志。"
                  : "No error log for this session.";

    public static string MessageClipboardFailed =>
        IsChinese ? "导出配置失败。" : "Failed to export config.";

    public static string LogServerStarted =>
        IsChinese ? "托盘服务已启动。" : "Tray service started.";

    public static string LogServerPaused =>
        IsChinese ? "服务已暂停。" : "Server paused.";

    public static string LogServerResumed =>
        IsChinese ? "服务已恢复。" : "Server resumed.";

    public static string LogClientConnected(string name, int pid) =>
        IsChinese ? $"客户端已连接: {name} (PID {pid})" : $"Client connected: {name} (PID {pid})";

    public static string LogClientDisconnected(string name, int pid) =>
        IsChinese ? $"客户端已断开: {name} (PID {pid})" : $"Client disconnected: {name} (PID {pid})";

    public static string LogClipboardFailed =>
        IsChinese ? "导出配置失败" : "Failed to export config";

    public static string LogClipboardFallback(string path) =>
        IsChinese ? $"剪贴板被占用，已写入临时配置文件: {path}" : $"Clipboard busy, wrote temporary config file: {path}";

    public static string TrayPaused =>
        IsChinese ? "SolidWorks MCP Server (已暂停)"
                  : "SolidWorks MCP Server (Paused)";

    public static string ClientsStatus(int n) =>
        n == 0 ? (IsChinese ? "连接: 无"    : "Clients: none")
               : (IsChinese ? $"连接: {n} 个" : $"Clients: {n}");
}
