using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using SolidWorksBridge.Models;
using SolidWorksBridge.PipeServer;

namespace SolidWorksBridge.Tests.PipeServer;

public class PipeServerManagerTests
{
    private const string TestPipeName = "SolidWorksMcpBridge_Test";

    // --- Message Framing Tests (static methods on MemoryStream) ---

    [Fact]
    public async Task WriteMessage_ReadMessage_Roundtrip()
    {
        var stream = new MemoryStream();

        var response = PipeResponse.Success("req-001", new { status = "ok" });
        await PipeServerManager.WriteMessageAsync(stream, response);

        stream.Position = 0;

        // ReadMessageAsync reads PipeRequest, but framing is the same.
        // We'll test framing at byte level instead.
        var lengthBuf = new byte[4];
        await stream.ReadAsync(lengthBuf);
        var length = BitConverter.ToInt32(lengthBuf, 0);

        Assert.True(length > 0);
        Assert.True(length < 1024);

        var bodyBuf = new byte[length];
        await stream.ReadAsync(bodyBuf);

        var json = Encoding.UTF8.GetString(bodyBuf);
        Assert.Contains("\"id\"", json);
        Assert.Contains("req-001", json);
    }

    [Fact]
    public async Task ReadMessage_ValidRequest_ReturnsCorrectObject()
    {
        var stream = new MemoryStream();
        var request = new PipeRequest
        {
            Id = "test-42",
            Method = "ping",
            Params = null
        };

        // Write with framing
        var bodyBytes = PipeMessageSerializer.Serialize(request);
        var lengthBytes = BitConverter.GetBytes(bodyBytes.Length);
        await stream.WriteAsync(lengthBytes);
        await stream.WriteAsync(bodyBytes);

        stream.Position = 0;
        var result = await PipeServerManager.ReadMessageAsync(stream);

        Assert.NotNull(result);
        Assert.Equal("test-42", result!.Id);
        Assert.Equal("ping", result.Method);
    }

    [Fact]
    public async Task ReadMessage_EmptyStream_ReturnsNull()
    {
        var stream = new MemoryStream(); // empty
        var result = await PipeServerManager.ReadMessageAsync(stream);
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadMessage_IncompleteLength_ThrowsEndOfStream()
    {
        var stream = new MemoryStream(new byte[] { 0x01, 0x00 }); // only 2 bytes
        await Assert.ThrowsAsync<EndOfStreamException>(
            () => PipeServerManager.ReadMessageAsync(stream));
    }

    [Fact]
    public async Task ReadMessage_InvalidLength_ThrowsInvalidOperation()
    {
        var stream = new MemoryStream();
        // Write a negative length
        await stream.WriteAsync(BitConverter.GetBytes(-1));
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PipeServerManager.ReadMessageAsync(stream));
    }

    [Fact]
    public async Task ReadMessage_ExcessiveLength_ThrowsInvalidOperation()
    {
        var stream = new MemoryStream();
        // Write length > 10MB
        await stream.WriteAsync(BitConverter.GetBytes(20 * 1024 * 1024));
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PipeServerManager.ReadMessageAsync(stream));
    }

    // --- Named Pipe Integration Tests ---

    [Fact]
    public async Task Server_AcceptsConnection_HandlesRequest_SendsResponse()
    {
        var pipeName = $"{TestPipeName}_{Guid.NewGuid():N}";
        var handlerCalled = false;

        Func<PipeRequest, Task<PipeResponse>> handler = req =>
        {
            handlerCalled = true;
            return Task.FromResult(PipeResponse.Success(req.Id, new { pong = true }));
        };

        using var server = new PipeServerManager(pipeName, handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Start server in background
        var serverTask = Task.Run(() => server.StartAsync(cts.Token));

        // Give server time to start listening
        await Task.Delay(100);

        // Connect as client
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        await client.ConnectAsync(cts.Token);

        // Send a request
        var request = new PipeRequest { Id = "c-001", Method = "ping" };
        var bodyBytes = PipeMessageSerializer.Serialize(request);
        await client.WriteAsync(BitConverter.GetBytes(bodyBytes.Length));
        await client.WriteAsync(bodyBytes);
        await client.FlushAsync();

        // Read response
        var lengthBuf = new byte[4];
        await ReadExactAsync(client, lengthBuf, cts.Token);
        var length = BitConverter.ToInt32(lengthBuf, 0);

        var responseBuf = new byte[length];
        await ReadExactAsync(client, responseBuf, cts.Token);

        var response = PipeMessageSerializer.Deserialize<PipeResponse>(responseBuf);

        Assert.True(handlerCalled);
        Assert.NotNull(response);
        Assert.Equal("c-001", response!.Id);
        Assert.Null(response.Error);

        // Clean disconnect
        client.Close();
        cts.Cancel();

        try { await serverTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task Server_HandlerThrows_ReturnsErrorResponse()
    {
        var pipeName = $"{TestPipeName}_{Guid.NewGuid():N}";

        Func<PipeRequest, Task<PipeResponse>> handler = _ =>
            throw new InvalidOperationException("Test error");

        using var server = new PipeServerManager(pipeName, handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var serverTask = Task.Run(() => server.StartAsync(cts.Token));
        await Task.Delay(100);

        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        await client.ConnectAsync(cts.Token);

        var request = new PipeRequest { Id = "err-001", Method = "crash" };
        var bodyBytes = PipeMessageSerializer.Serialize(request);
        await client.WriteAsync(BitConverter.GetBytes(bodyBytes.Length));
        await client.WriteAsync(bodyBytes);
        await client.FlushAsync();

        var lengthBuf = new byte[4];
        await ReadExactAsync(client, lengthBuf, cts.Token);
        var length = BitConverter.ToInt32(lengthBuf, 0);

        var responseBuf = new byte[length];
        await ReadExactAsync(client, responseBuf, cts.Token);

        var response = PipeMessageSerializer.Deserialize<PipeResponse>(responseBuf);

        Assert.NotNull(response);
        Assert.Equal("err-001", response!.Id);
        Assert.NotNull(response.Error);
        Assert.Equal(PipeErrorCodes.InternalError, response.Error!.Code);
        Assert.Contains("Test error", response.Error.Message);

        client.Close();
        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task Server_MultipleMessages_AllHandledCorrectly()
    {
        var pipeName = $"{TestPipeName}_{Guid.NewGuid():N}";
        var messageCount = 0;

        Func<PipeRequest, Task<PipeResponse>> handler = req =>
        {
            Interlocked.Increment(ref messageCount);
            return Task.FromResult(PipeResponse.Success(req.Id, new { seq = messageCount }));
        };

        using var server = new PipeServerManager(pipeName, handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var serverTask = Task.Run(() => server.StartAsync(cts.Token));
        await Task.Delay(100);

        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        await client.ConnectAsync(cts.Token);

        // Send 3 sequential messages
        for (int i = 1; i <= 3; i++)
        {
            var request = new PipeRequest { Id = $"multi-{i}", Method = "test" };
            var bodyBytes = PipeMessageSerializer.Serialize(request);
            await client.WriteAsync(BitConverter.GetBytes(bodyBytes.Length));
            await client.WriteAsync(bodyBytes);
            await client.FlushAsync();

            var lengthBuf = new byte[4];
            await ReadExactAsync(client, lengthBuf, cts.Token);
            var length = BitConverter.ToInt32(lengthBuf, 0);

            var responseBuf = new byte[length];
            await ReadExactAsync(client, responseBuf, cts.Token);

            var response = PipeMessageSerializer.Deserialize<PipeResponse>(responseBuf);

            Assert.NotNull(response);
            Assert.Equal($"multi-{i}", response!.Id);
        }

        Assert.Equal(3, messageCount);

        client.Close();
        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public void Constructor_NullPipeName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PipeServerManager(null!, _ => Task.FromResult(PipeResponse.Success(""))));
    }

    [Fact]
    public void Constructor_NullHandler_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PipeServerManager("test", null!));
    }

    // --- Helper ---

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0)
                throw new EndOfStreamException();
            totalRead += read;
        }
    }
}
