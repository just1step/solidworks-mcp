using SolidWorksBridge;
using SolidWorksBridge.PipeServer;

namespace SolidWorksBridge;

public class Program
{
    public const string AppName = "SolidWorksBridge";
    public const string PipeName = "SolidWorksMcpBridge";

    public static void Main(string[] args)
    {
        // COM requires STA thread
        var thread = new Thread(RunSta);
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private static void RunSta()
    {
        Console.WriteLine($"{AppName} starting on STA thread...");

        using var cts = new CancellationTokenSource();

        // Graceful shutdown on Ctrl+C or SIGTERM
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("Shutdown requested...");
            cts.Cancel();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

        // Wire dependencies and register all handlers
        var bootstrapper = AppBootstrapper.CreateProduction();
        bootstrapper.RegisterHandlers();

        // Start pipe server
        var server = new PipeServerManager(PipeName, bootstrapper.MessageHandler.HandleAsync);

        Console.WriteLine($"Listening on pipe: {PipeName}");

        try
        {
            server.StartAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }

        Console.WriteLine($"{AppName} stopped.");
    }
}
