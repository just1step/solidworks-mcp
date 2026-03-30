using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using SolidWorksBridge.Models;

namespace SolidWorksBridge.PipeServer;

/// <summary>
/// Async Named Pipe server that listens for JSON messages with length-prefix framing.
/// Protocol: [4-byte LE length][UTF-8 JSON body]
/// </summary>
public class PipeServerManager : IDisposable
{
    private readonly string _pipeName;
    private readonly Func<PipeRequest, Task<PipeResponse>> _handler;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public PipeServerManager(string pipeName, Func<PipeRequest, Task<PipeResponse>> handler)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public bool IsListening { get; private set; }

    /// <summary>
    /// Start accepting client connections in a loop.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsListening = true;

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1, // max 1 concurrent connection (SolidWorks is single-threaded)
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                try
                {
                    await pipeServer.WaitForConnectionAsync(_cts.Token);
                    await HandleClientAsync(pipeServer, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[PipeServer] Client error: {ex.Message}");
                }
                finally
                {
                    if (pipeServer.IsConnected)
                        pipeServer.Disconnect();
                    await pipeServer.DisposeAsync();
                }
            }
        }
        finally
        {
            IsListening = false;
        }
    }

    /// <summary>
    /// Handle a single connected client: read messages, process, write responses.
    /// </summary>
    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        while (pipe.IsConnected && !ct.IsCancellationRequested)
        {
            PipeRequest? request;
            try
            {
                request = await ReadMessageAsync(pipe, ct);
            }
            catch (EndOfStreamException)
            {
                break; // Client disconnected
            }
            catch (IOException)
            {
                break; // Pipe broken
            }

            if (request == null)
                break;

            PipeResponse response;
            try
            {
                response = await _handler(request);
            }
            catch (Exception ex)
            {
                response = PipeResponse.Failure(
                    request.Id,
                    PipeErrorCodes.InternalError,
                    $"Handler exception: {ex.Message}");
            }

            try
            {
                await WriteMessageAsync(pipe, response, ct);
            }
            catch (IOException)
            {
                break; // Pipe broken
            }
        }
    }

    /// <summary>
    /// Read a length-prefixed JSON message from the pipe.
    /// </summary>
    public static async Task<PipeRequest?> ReadMessageAsync(Stream stream, CancellationToken ct = default)
    {
        // Read 4-byte little-endian length prefix
        var lengthBuffer = new byte[4];
        var bytesRead = await ReadExactAsync(stream, lengthBuffer, ct);
        if (bytesRead == 0)
            return null; // Clean disconnect

        if (bytesRead < 4)
            throw new EndOfStreamException("Incomplete length prefix");

        var length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length <= 0 || length > 10 * 1024 * 1024) // Max 10MB
            throw new InvalidOperationException($"Invalid message length: {length}");

        // Read JSON body
        var bodyBuffer = new byte[length];
        bytesRead = await ReadExactAsync(stream, bodyBuffer, ct);
        if (bytesRead < length)
            throw new EndOfStreamException("Incomplete message body");

        return PipeMessageSerializer.Deserialize<PipeRequest>(bodyBuffer);
    }

    /// <summary>
    /// Write a length-prefixed JSON message to the pipe.
    /// </summary>
    public static async Task WriteMessageAsync(Stream stream, PipeResponse response, CancellationToken ct = default)
    {
        var bodyBytes = PipeMessageSerializer.Serialize(response);
        var lengthBytes = BitConverter.GetBytes(bodyBytes.Length);

        await stream.WriteAsync(lengthBytes, ct);
        await stream.WriteAsync(bodyBytes, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>
    /// Read exactly the requested number of bytes, or return 0 on clean EOF.
    /// </summary>
    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0)
                return totalRead; // EOF
            totalRead += read;
        }
        return totalRead;
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}
