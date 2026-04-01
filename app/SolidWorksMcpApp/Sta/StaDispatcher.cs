using System.Collections.Concurrent;
using System.Text.Json;
using SolidWorksMcpApp.Logging;

namespace SolidWorksMcpApp;

/// <summary>
/// Dispatches work to a dedicated STA thread required for SolidWorks COM interop.
/// All SolidWorks API calls must run on the same STA thread where COM objects were created.
/// The internal blocking queue already serializes all requests from all connected clients.
/// </summary>
public sealed class StaDispatcher : IDisposable
{
    private readonly Thread _thread;
    private readonly BlockingCollection<Action> _queue = new();

    public StaDispatcher()
    {
        _thread = new Thread(() =>
        {
            foreach (var action in _queue.GetConsumingEnumerable())
                action();
        })
        {
            IsBackground = true,
            Name = "SW-STA"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>Invoke a function on the shared STA queue.</summary>
    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (ServerState.IsPaused)
            return Task.FromException<T>(
                new InvalidOperationException("服务已暂停，请右键托盘图标选择【启动】恢复。"));

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try   { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    /// <summary>Invoke an action on the STA thread and await its completion.</summary>
    public Task InvokeAsync(Action action) =>
        InvokeAsync<object?>(() => { action(); return null; });

    public async Task<T> InvokeLoggedAsync<T>(string operationName, object? arguments, Func<T> func)
    {
        string argumentText = FormatPayload(arguments);
        ServerLogBuffer.Append("INFO", "COM", $"{operationName} started{argumentText}.");

        try
        {
            var result = await InvokeAsync(func);
            string resultText = FormatPayload(result);
            ServerLogBuffer.Append("INFO", "COM", $"{operationName} completed{resultText}.");
            return result;
        }
        catch (Exception ex)
        {
            ServerLogBuffer.Append("ERROR", "COM", $"{operationName} failed{argumentText}.", ex);
            throw;
        }
    }

    public Task InvokeLoggedAsync(string operationName, object? arguments, Action action) =>
        InvokeLoggedAsync<object?>(operationName, arguments, () =>
        {
            action();
            return null;
        });

    public void Dispose() => _queue.CompleteAdding();

    private static string FormatPayload(object? payload)
    {
        if (payload is null)
        {
            return string.Empty;
        }

        try
        {
            string json = JsonSerializer.Serialize(payload);
            if (string.IsNullOrWhiteSpace(json) || json == "{}" || json == "[]" || json == "null")
            {
                return string.Empty;
            }

            const int maxLength = 1200;
            if (json.Length > maxLength)
            {
                json = json[..maxLength] + "...(truncated)";
            }

            return $" | payload={json}";
        }
        catch
        {
            return $" | payload={payload}";
        }
    }
}
