using SolidWorksBridge.Models;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.PipeServer;

/// <summary>
/// Routes incoming PipeRequest messages to registered method handlers.
/// Each handler is an async function: (PipeRequest) => object? (result payload).
/// </summary>
public class MessageHandler
{
    private readonly Dictionary<string, Func<PipeRequest, Task<object?>>> _handlers = new();

    /// <summary>
    /// Register a handler for a specific method name.
    /// </summary>
    public void Register(string method, Func<PipeRequest, Task<object?>> handler)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[method] = handler;
    }

    /// <summary>
    /// Check if a method is registered.
    /// </summary>
    public bool HasMethod(string method) => _handlers.ContainsKey(method);

    /// <summary>
    /// Get all registered method names.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredMethods => _handlers.Keys;

    /// <summary>
    /// Dispatch a PipeRequest to its handler and return a PipeResponse.
    /// - Unknown method → MethodNotFound error
    /// - Handler throws → InternalError with exception message
    /// - Handler succeeds → Success with result
    /// </summary>
    public async Task<PipeResponse> HandleAsync(PipeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.Method))
        {
            return PipeResponse.Failure(
                request.Id,
                PipeErrorCodes.InvalidParams,
                "Method name is required");
        }

        // Built-in ping handler
        if (request.Method == "ping")
        {
            return PipeResponse.Success(request.Id, new { pong = true, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        if (!_handlers.TryGetValue(request.Method, out var handler))
        {
            return PipeResponse.Failure(
                request.Id,
                PipeErrorCodes.MethodNotFound,
                $"Unknown method: {request.Method}");
        }

        try
        {
            var result = await handler(request);
            return PipeResponse.Success(request.Id, result);
        }
        catch (SolidWorksApiException ex)
        {
            return PipeResponse.Failure(
                request.Id,
                ex.PipeErrorCode,
                ex.Message,
                ex.ToErrorData());
        }
        catch (Exception ex)
        {
            return PipeResponse.Failure(
                request.Id,
                PipeErrorCodes.InternalError,
                $"Handler error: {ex.Message}");
        }
    }
}
