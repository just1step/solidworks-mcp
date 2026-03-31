using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SolidWorksBridge.SolidWorks;
using SolidWorksMcpApp;
using SolidWorksMcpApp.Ipc;
using SolidWorksMcpApp.Logging;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

// Explicit Main is required so [STAThread] can be applied for WinForms.
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex)
        {
            var t = Path.Combine(Path.GetTempPath(), "sw_mcp_crash.txt");
            File.WriteAllText(t, ex.ToString());
            MessageBox.Show(ex.Message + "\n\nDetails written to:\n" + t,
                "SolidWorks MCP – Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static void Run(string[] args)
    {
        // ── Proxy mode (spawned by VS Code / Claude Desktop) ─────────────
        // MCP clients pass --proxy; the exe relays stdin/stdout to the Hub.
        if (args.Contains("--proxy")) { RunProxy(args); return; }

        // ── Tray / Hub mode (double-click, one singleton) ─────────────────
        ServerState.InitLogFile();

        using var mutex = new Mutex(true, "Global\\SolidWorksMcpServer_TrayInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(Strings.AlreadyRunning, Strings.AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        using var cts = new CancellationTokenSource();

        // Shared services: one StaDispatcher + one Bridge connection for ALL sessions.
        var sharedSvc = BuildSharedServices();

        // Hub pipe server: each connecting Proxy gets a full MCP session on its pipe.
        new HubPipeServer(cts.Token, async (pipe, client, ct) =>
        {
            var sessionHost = BuildMcpSessionHost(pipe, sharedSvc);
            try   { await sessionHost.RunAsync(ct); }
            catch (OperationCanceledException) { /* Hub shutting down */ }
            catch { /* client disconnected */ }
        }).Start();

        using var tray = new TrayApplicationContext(cts);
        Application.Run(tray);

        // Tray closed → cancel everything.
        cts.Cancel();
        if (sharedSvc is IDisposable d) d.Dispose();
    }

    // ── Proxy mode ───────────────────────────────────────────────────────

    static void RunProxy(string[] args)
    {
        var clientName = GetArg(args, "--client") ?? "MCP Client";

        // Connect to Hub; if it is not running yet, auto-start it and retry.
        var pipe = ConnectToHubOrStart();
        if (pipe is null)
        {
            Environment.Exit(1);
            return;
        }

        // Send connect handshake.
        var connectMsg  = JsonSerializer.Serialize(new { type = "connect", clientName, pid = Environment.ProcessId });
        var connectBytes = Encoding.UTF8.GetBytes(connectMsg + "\n");
        pipe.Write(connectBytes);
        pipe.Flush();

        // Wait for "ready" — read synchronously one byte at a time so we
        // don't accidentally buffer the first MCP bytes.
        var readyLine = ReadLineSync(pipe);
        if (readyLine is null || !readyLine.Contains("\"ready\""))
        {
            pipe.Dispose();
            Environment.Exit(1);
            return;
        }

        // Relay: stdin → pipe (MCP requests) and pipe → stdout (MCP responses), concurrently.
        var stdin  = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();
        var t1 = stdin.CopyToAsync(pipe);
        var t2 = pipe.CopyToAsync(stdout);
        Task.WhenAny(t1, t2).GetAwaiter().GetResult();

        pipe.Dispose();
    }

    // ── Shared services (STA + Bridge) — one set per Hub process ─────────

    static IServiceProvider BuildSharedServices()
    {
        var sc = new ServiceCollection();
        sc.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddProvider(new ErrorFileLoggerProvider(ServerState.LogFilePath));
        });

        sc.AddSingleton<StaDispatcher>();

        sc.AddSingleton<ISwConnectionManager>(sp =>
        {
            var sta = sp.GetRequiredService<StaDispatcher>();
            return sta.InvokeAsync(() =>
            {
                var connector = new SwComConnector();
                return (ISwConnectionManager)new SwConnectionManager(connector);
            }).GetAwaiter().GetResult();
        });

        sc.AddSingleton<IDocumentService>(sp  => new DocumentService(sp.GetRequiredService<ISwConnectionManager>()));
        sc.AddSingleton<ISelectionService>(sp => new SelectionService(sp.GetRequiredService<ISwConnectionManager>()));
        sc.AddSingleton<ISketchService>(sp    => new SketchService(sp.GetRequiredService<ISwConnectionManager>()));
        sc.AddSingleton<IFeatureService>(sp   => new FeatureService(sp.GetRequiredService<ISwConnectionManager>()));
        sc.AddSingleton<IAssemblyService>(sp  => new AssemblyService(sp.GetRequiredService<ISwConnectionManager>()));

        return sc.BuildServiceProvider();
    }

    // ── Per-session MCP host (one per connected Proxy client) ────────────

    static IHost BuildMcpSessionHost(System.IO.Stream pipeStream, IServiceProvider sharedSvc)
    {
        var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ErrorFileLoggerProvider(ServerState.LogFilePath));

        // Re-use the shared singleton instances so all clients share one STA queue and one COM world.
        builder.Services.AddSingleton(sharedSvc.GetRequiredService<StaDispatcher>());
        builder.Services.AddSingleton(sharedSvc.GetRequiredService<ISwConnectionManager>());
        builder.Services.AddSingleton(sharedSvc.GetRequiredService<IDocumentService>());
        builder.Services.AddSingleton(sharedSvc.GetRequiredService<ISelectionService>());
        builder.Services.AddSingleton(sharedSvc.GetRequiredService<ISketchService>());
        builder.Services.AddSingleton(sharedSvc.GetRequiredService<IFeatureService>());
        builder.Services.AddSingleton(sharedSvc.GetRequiredService<IAssemblyService>());

        builder.Services.AddTransient<SolidWorksMcpApp.Tools.ConnectionTools>();
        builder.Services.AddTransient<SolidWorksMcpApp.Tools.DocumentTools>();
        builder.Services.AddTransient<SolidWorksMcpApp.Tools.SelectionTools>();
        builder.Services.AddTransient<SolidWorksMcpApp.Tools.SketchTools>();
        builder.Services.AddTransient<SolidWorksMcpApp.Tools.FeatureTools>();
        builder.Services.AddTransient<SolidWorksMcpApp.Tools.AssemblyTools>();

        builder.Services
            .AddMcpServer()
            .WithStreamServerTransport(pipeStream, pipeStream)   // bidirectional pipe
            .WithToolsFromAssembly();

        return builder.Build();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Reads one \n-terminated line from the stream synchronously, one byte at a time.
    /// Only used during the startup handshake where latency is irrelevant.
    /// </summary>
    static string? ReadLineSync(System.IO.Stream stream)
    {
        var buf = new List<byte>(128);
        var b   = new byte[1];
        while (true)
        {
            int n = stream.Read(b, 0, 1);
            if (n == 0) return null;
            if (b[0] == (byte)'\n') return Encoding.UTF8.GetString(buf.ToArray()).TrimEnd('\r');
            buf.Add(b[0]);
        }
    }

    static string? GetArg(string[] args, string name)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    static NamedPipeClientStream? ConnectToHubOrStart()
    {
        var pipe = TryConnectToHub(800);
        if (pipe is not null) return pipe;

        using var startMutex = new Mutex(false, "Global\\SolidWorksMcpServer_AutoStart");
        bool ownsStartMutex = false;
        try
        {
            ownsStartMutex = startMutex.WaitOne(TimeSpan.FromSeconds(12));

            pipe = TryConnectToHub(500);
            if (pipe is not null) return pipe;

            if (ownsStartMutex)
                StartHubProcess();

            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (DateTime.UtcNow < deadline)
            {
                pipe = TryConnectToHub(1000);
                if (pipe is not null) return pipe;

                Thread.Sleep(250);
            }
        }
        finally
        {
            if (ownsStartMutex)
                startMutex.ReleaseMutex();
        }

        return null;
    }

    static NamedPipeClientStream? TryConnectToHub(int timeoutMs)
    {
        var pipe = new NamedPipeClientStream(".", HubPipeServer.PipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            pipe.Connect(timeoutMs);
            return pipe;
        }
        catch
        {
            pipe.Dispose();
            return null;
        }
    }

    static void StartHubProcess()
    {
        var exePath = Environment.ProcessPath
            ?? System.Reflection.Assembly.GetExecutingAssembly().Location;

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? Directory.GetCurrentDirectory(),
        });
    }
}
