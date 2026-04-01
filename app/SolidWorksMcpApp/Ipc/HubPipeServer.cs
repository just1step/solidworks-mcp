using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using SolidWorksMcpApp.Logging;

namespace SolidWorksMcpApp.Ipc;

/// <summary>
/// Named-pipe server running in Hub (Tray) mode.
///
/// Protocol per connection:
///   1. Proxy → Hub  {"type":"connect","clientName":"VS Code","pid":1234}\n
///   2. Hub  → Proxy {"type":"ready","sessionId":"abc12345"}\n
///   3. After that, the pipe carries raw MCP JSON-RPC (stdio transport relay).
/// </summary>
internal sealed class HubPipeServer
{
    public const string PipeName = "SolidWorksMcpHub";

    private readonly CancellationToken _ct;

    /// <summary>
    /// Called after the handshake.  The session runner owns the pipe stream
    /// until it returns; cleanup (ClientRegistry.Remove) happens in the
    /// finally block of HandleClientAsync, not inside the runner.
    /// </summary>
    private readonly Func<NamedPipeServerStream, ClientInfo, CancellationToken, Task> _sessionRunner;

    public HubPipeServer(
        CancellationToken ct,
        Func<NamedPipeServerStream, ClientInfo, CancellationToken, Task> sessionRunner)
    {
        _ct            = ct;
        _sessionRunner = sessionRunner;
    }

    /// <summary>Starts the accept loop on a thread-pool thread and returns immediately.</summary>
    public void Start()
    {
        ServerLogBuffer.Append("INFO", "Hub", $"Hub pipe server starting on '{PipeName}'.");
        Task.Run(AcceptLoopAsync);
    }

    // ── Accept loop ──────────────────────────────────────────────────────

    private async Task AcceptLoopAsync()
    {
        while (!_ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(_ct);
                ServerLogBuffer.Append("INFO", "Hub", "Inbound hub pipe connection established.");

                // Handle each client concurrently; cleanup guaranteed by finally.
                _ = HandleClientAsync(pipe);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                ServerLogBuffer.Append("ERROR", "Hub", "Hub accept loop iteration failed.", ex);
                pipe?.Dispose();
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        ServerLogBuffer.Append("INFO", "Hub", "Hub pipe server stopped.");
    }

    // ── Per-client handler ───────────────────────────────────────────────

    private async Task HandleClientAsync(NamedPipeServerStream pipe)
    {
        ClientInfo? client = null;
        try
        {
            // ── 1. Read connect handshake ─────────────────────────────────
            var connectLine = await ReadLineAsync(pipe, _ct);
            if (connectLine is null)
            {
                ServerLogBuffer.Append("WARN", "Hub", "Hub client disconnected before sending the connect handshake.");
                return;
            }

            JsonElement msg;
            try { msg = JsonSerializer.Deserialize<JsonElement>(connectLine); }
            catch (Exception ex)
            {
                ServerLogBuffer.Append("WARN", "Hub", $"Invalid hub connect handshake: {connectLine}", ex);
                return;
            }

            if (GetString(msg, "type") != "connect")
            {
                ServerLogBuffer.Append("WARN", "Hub", $"Unexpected first hub message: {connectLine}");
                return;
            }

            client = new ClientInfo
            {
                Name = GetString(msg, "clientName") ?? "Unknown",
                Pid  = msg.TryGetProperty("pid", out var p) && p.ValueKind == JsonValueKind.Number
                       ? p.GetInt32() : 0,
            };
            ClientRegistry.Add(client);
            ServerLogBuffer.Append("INFO", "Hub", $"Hub handshake received from {client.Name} (PID {client.Pid}, session {client.SessionId}).");

            // ── 2. Send ready ─────────────────────────────────────────────
            var readyBytes = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new { type = "ready", sessionId = client.SessionId }) + "\n");
            await pipe.WriteAsync(readyBytes, _ct);
            await pipe.FlushAsync(_ct);
            ServerLogBuffer.Append("INFO", "Hub", $"Hub ready sent for session {client.SessionId}.");

            // ── 3. Run full MCP session on this pipe ──────────────────────
            // The session runner uses WithStreamServerTransport(pipe, pipe).
            ServerLogBuffer.Append("INFO", "Hub", $"Starting MCP session {client.SessionId} for {client.Name}.");
            await _sessionRunner(pipe, client, _ct);
            ServerLogBuffer.Append("INFO", "Hub", $"MCP session {client.SessionId} ended.");
        }
        catch (Exception ex)
        {
            var sessionId = client?.SessionId ?? "<unknown>";
            ServerLogBuffer.Append("ERROR", "Hub", $"Hub client session {sessionId} failed.", ex);
        }
        finally
        {
            if (client is not null) ClientRegistry.Remove(client.SessionId);
            try { pipe.Dispose(); } catch { }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Reads one \n-terminated line from the stream one byte at a time so
    /// we never buffer ahead into MCP relay data territory.
    /// </summary>
    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var buf     = new List<byte>(256);
        var oneByte = new byte[1];
        while (true)
        {
            int n;
            try { n = await stream.ReadAsync(oneByte, ct); }
            catch { return null; }
            if (n == 0) return null;
            if (oneByte[0] == (byte)'\n')
                return Encoding.UTF8.GetString(buf.ToArray()).TrimEnd('\r');
            buf.Add(oneByte[0]);
        }
    }

    private static string? GetString(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}
