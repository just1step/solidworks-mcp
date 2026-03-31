using Microsoft.Extensions.Hosting;
using SolidWorksMcpApp.Ipc;
using SolidWorksMcpApp.Logging;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SolidWorksMcpApp;

internal sealed class TrayApplicationContext : ApplicationContext, IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _clientsItem;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly Icon _appIcon;
    private MonitorForm? _monitorForm;
    // Hidden control used only for UI-thread marshaling of ClientRegistry.Changed events.
    private readonly Control _uiInvoker = new();

    public TrayApplicationContext(CancellationTokenSource cts)
    {
        _cts = cts;
        _uiInvoker.CreateControl();

        _statusItem  = new ToolStripMenuItem(Strings.StatusRunning)         { Enabled = false };
        _clientsItem = new ToolStripMenuItem(Strings.ClientsStatus(0)) { Enabled = false };
        _startItem   = new ToolStripMenuItem(Strings.MenuStart, null, OnStart);
        _pauseItem   = new ToolStripMenuItem(Strings.MenuPause, null, OnPause);
        _appIcon     = LoadTrayIcon();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_clientsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Strings.MenuMonitor, null, OnOpenMonitor));
        menu.Items.Add(_startItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Strings.MenuVsCode,  null, OnCopyVsCodeConfig));
        menu.Items.Add(new ToolStripMenuItem(Strings.MenuClaude,  null, OnCopyClaudeConfig));
        menu.Items.Add(new ToolStripMenuItem(Strings.MenuOpenLog, null, OnOpenLog));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Strings.MenuExit, null, OnExit));

        _trayIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "SolidWorks MCP Server",
            ContextMenuStrip = menu,
            Visible = true,
        };

        ClientRegistry.Changed += OnClientsChanged;
        UpdateMenuState();
        ServerLogBuffer.Append("INFO", "App", Strings.LogServerStarted);
        Task.Run(AutoConfigService.WriteConfigs);
    }

    private void OnClientsChanged()
    {
        if (_uiInvoker.InvokeRequired)
            _uiInvoker.BeginInvoke(UpdateMenuState);
        else
            UpdateMenuState();
    }

    private void OnStart(object? sender, EventArgs e)
    {
        ServerState.IsPaused = false;
        UpdateMenuState();
        ServerLogBuffer.Append("INFO", "App", Strings.LogServerResumed);
        _trayIcon.ShowBalloonTip(1500, Strings.MenuStart, Strings.BalloonStarted, ToolTipIcon.Info);
    }

    private void OnPause(object? sender, EventArgs e)
    {
        ServerState.IsPaused = true;
        UpdateMenuState();
        ServerLogBuffer.Append("WARN", "App", Strings.LogServerPaused);
        _trayIcon.ShowBalloonTip(1500, Strings.MenuPause, Strings.BalloonPaused, ToolTipIcon.Warning);
    }

    private void OnOpenMonitor(object? sender, EventArgs e)
    {
        if (_monitorForm is null || _monitorForm.IsDisposed)
            _monitorForm = new MonitorForm();

        if (!_monitorForm.Visible)
            _monitorForm.Show();

        if (_monitorForm.WindowState == FormWindowState.Minimized)
            _monitorForm.WindowState = FormWindowState.Normal;

        _monitorForm.BringToFront();
        _monitorForm.Activate();
    }

    private void OnCopyClaudeConfig(object? sender, EventArgs e)
    {
        var exePath = GetExePath();
        var json = $$"""
            {
              "mcpServers": {
                "solidworks": {
                  "command": "{{EscapeJson(exePath)}}",
                  "args": ["--proxy", "--client", "Claude Desktop"]
                }
              }
            }
            """;
                ExportConfig(json, Strings.BalloonClaudeCopied, "claude-desktop");
    }

    private void OnCopyVsCodeConfig(object? sender, EventArgs e)
    {
        var exePath = GetExePath();
        var json = $$"""
            {
              "mcp": {
                "servers": {
                  "solidworks": {
                    "type": "stdio",
                    "command": "{{EscapeJson(exePath)}}",
                    "args": ["--proxy", "--client", "VS Code"]
                  }
                }
              }
            }
            """;
                ExportConfig(json, Strings.BalloonVsCodeCopied, "vscode");
    }

    private void OnOpenLog(object? sender, EventArgs e)
    {
        var logPath = ServerState.LogFilePath;
        if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
        {
            // Open in default text editor (usually Notepad).
            Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
            return;
        }

        // No errors in this session — open the logs folder instead.
        var exeDir = Path.GetDirectoryName(GetExePath())!;
        var logsDir = Path.Combine(exeDir, "logs");
        if (Directory.Exists(logsDir))
            Process.Start("explorer.exe", logsDir);
        else
            _trayIcon.ShowBalloonTip(2000, Strings.BalloonNoLog, Strings.BalloonNoLogBody, ToolTipIcon.Info);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _monitorForm?.Close();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    private void UpdateMenuState()
    {
        bool paused = ServerState.IsPaused;
        _statusItem.Text     = paused ? Strings.StatusPaused : Strings.StatusRunning;
        _clientsItem.Text    = Strings.ClientsStatus(ClientRegistry.Count);
        _startItem.Enabled   = paused;
        _pauseItem.Enabled   = !paused;
        _trayIcon.Text       = paused ? Strings.TrayPaused : Strings.AppTitle;
    }

    private static string GetExePath() =>
        Environment.ProcessPath ?? throw new InvalidOperationException("Process path is unavailable.");

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static Icon LoadTrayIcon()
    {
        const string resourceName = "SolidWorksMcpApp.TrayIconPng";

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
            return new Icon(SystemIcons.Application, 16, 16);

        using var bitmap = new Bitmap(stream);
        var hIcon = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(hIcon);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private void ExportConfig(string text, string successMessage, string fileStem)
    {
        try
        {
            Clipboard.SetDataObject(text, true, 10, 100);
            ServerLogBuffer.Append("INFO", "App", successMessage);
            _trayIcon.ShowBalloonTip(2000, Strings.BalloonCopied, successMessage, ToolTipIcon.Info);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            var path = WriteFallbackConfigFile(fileStem, text);
            ServerLogBuffer.Append("WARN", "App", Strings.LogClipboardFallback(path));
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            _trayIcon.ShowBalloonTip(3000, Strings.BalloonClipboardBusy, Strings.BalloonClipboardFallback, ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            ServerLogBuffer.Append("ERROR", "App", $"{Strings.LogClipboardFailed}: {ex.Message}");
            MessageBox.Show(
                Strings.MessageClipboardFailed + "\n\n" + ex.Message,
                Strings.AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static string WriteFallbackConfigFile(string fileStem, string text)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"solidworks-mcp-{fileStem}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(path, text);
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClientRegistry.Changed -= OnClientsChanged;
            _monitorForm?.Dispose();
            _uiInvoker.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _appIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}