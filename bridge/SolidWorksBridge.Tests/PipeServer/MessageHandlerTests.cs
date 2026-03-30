using SolidWorksBridge.Models;
using SolidWorksBridge.PipeServer;

namespace SolidWorksBridge.Tests.PipeServer;

public class MessageHandlerTests
{
    // --- Test 1: Registered handler is actually called and returns success ---
    [Fact]
    public async Task HandleAsync_RegisteredMethod_CallsHandlerAndReturnsSuccess()
    {
        var handler = new MessageHandler();
        var handlerWasCalled = false;

        handler.Register("test_method", async (req) =>
        {
            handlerWasCalled = true;
            await Task.CompletedTask;
            return new { value = 42 };
        });

        var request = new PipeRequest { Id = "req-001", Method = "test_method" };
        var response = await handler.HandleAsync(request);

        Assert.True(handlerWasCalled, "Handler function was never called");
        Assert.Equal("req-001", response.Id);
        Assert.NotNull(response.Result);
        Assert.Null(response.Error);
    }

    // --- Test 2: Unknown method returns MethodNotFound error ---
    [Fact]
    public async Task HandleAsync_UnknownMethod_ReturnsMethodNotFoundError()
    {
        var handler = new MessageHandler();
        // No handlers registered

        var request = new PipeRequest { Id = "req-002", Method = "nonexistent" };
        var response = await handler.HandleAsync(request);

        Assert.Equal("req-002", response.Id);
        Assert.Null(response.Result);
        Assert.NotNull(response.Error);
        Assert.Equal(PipeErrorCodes.MethodNotFound, response.Error!.Code);
        Assert.Contains("nonexistent", response.Error.Message);
    }

    // --- Test 3: Empty method returns InvalidParams error ---
    [Fact]
    public async Task HandleAsync_EmptyMethod_ReturnsInvalidParamsError()
    {
        var handler = new MessageHandler();

        var request = new PipeRequest { Id = "req-003", Method = "" };
        var response = await handler.HandleAsync(request);

        Assert.Equal("req-003", response.Id);
        Assert.NotNull(response.Error);
        Assert.Equal(PipeErrorCodes.InvalidParams, response.Error!.Code);
    }

    // --- Test 4: Handler throws → InternalError with exception message ---
    [Fact]
    public async Task HandleAsync_HandlerThrows_ReturnsInternalError()
    {
        var handler = new MessageHandler();
        handler.Register("crasher", (req) =>
        {
            throw new InvalidOperationException("boom");
        });

        var request = new PipeRequest { Id = "req-004", Method = "crasher" };
        var response = await handler.HandleAsync(request);

        Assert.Equal("req-004", response.Id);
        Assert.Null(response.Result);
        Assert.NotNull(response.Error);
        Assert.Equal(PipeErrorCodes.InternalError, response.Error!.Code);
        Assert.Contains("boom", response.Error.Message);
    }

    // --- Test 5: Built-in ping responds without registration ---
    [Fact]
    public async Task HandleAsync_PingMethod_ReturnsPongWithoutRegistration()
    {
        var handler = new MessageHandler();
        // No handlers registered — ping is built-in

        var request = new PipeRequest { Id = "req-005", Method = "ping" };
        var response = await handler.HandleAsync(request);

        Assert.Equal("req-005", response.Id);
        Assert.NotNull(response.Result);
        Assert.Null(response.Error);
    }

    // --- Test 6: Handler returning null → Success with null result ---
    [Fact]
    public async Task HandleAsync_HandlerReturnsNull_SuccessWithNullResult()
    {
        var handler = new MessageHandler();
        handler.Register("noop", async (req) =>
        {
            await Task.CompletedTask;
            return null;
        });

        var request = new PipeRequest { Id = "req-006", Method = "noop" };
        var response = await handler.HandleAsync(request);

        Assert.Equal("req-006", response.Id);
        Assert.Null(response.Result);
        Assert.Null(response.Error);
    }

    // --- Test 7: HasMethod returns true for registered methods ---
    [Fact]
    public void HasMethod_ReturnsTrueForRegistered()
    {
        var handler = new MessageHandler();
        handler.Register("foo", _ => Task.FromResult<object?>(null));

        Assert.True(handler.HasMethod("foo"));
    }

    // --- Test 8: HasMethod returns false for unregistered methods ---
    [Fact]
    public void HasMethod_ReturnsFalseForUnregistered()
    {
        var handler = new MessageHandler();

        Assert.False(handler.HasMethod("bar"));
    }

    // --- Test 9: RegisteredMethods reflects all registrations ---
    [Fact]
    public void RegisteredMethods_ReflectsRegistrations()
    {
        var handler = new MessageHandler();
        handler.Register("alpha", _ => Task.FromResult<object?>(null));
        handler.Register("beta", _ => Task.FromResult<object?>(null));

        var methods = handler.RegisteredMethods;

        Assert.Equal(2, methods.Count);
        Assert.Contains("alpha", methods);
        Assert.Contains("beta", methods);
    }

    // --- Test 10: Register with null method throws ---
    [Fact]
    public void Register_NullMethod_ThrowsArgumentNull()
    {
        var handler = new MessageHandler();

        Assert.Throws<ArgumentNullException>(() =>
            handler.Register(null!, _ => Task.FromResult<object?>(null)));
    }

    // --- Test 11: HandleAsync with null request throws ---
    [Fact]
    public async Task HandleAsync_NullRequest_ThrowsArgumentNull()
    {
        var handler = new MessageHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.HandleAsync(null!));
    }

    // --- Test 12: Response ID matches request ID ---
    [Fact]
    public async Task HandleAsync_PreservesRequestId()
    {
        var handler = new MessageHandler();
        handler.Register("echo_id", async (req) =>
        {
            await Task.CompletedTask;
            return new { echoedId = req.Id };
        });

        var request = new PipeRequest { Id = "abc-123", Method = "echo_id" };
        var response = await handler.HandleAsync(request);

        Assert.Equal("abc-123", response.Id);
    }
}
