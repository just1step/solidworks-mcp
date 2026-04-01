using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolidWorksBridge.Models;

/// <summary>
/// Request message sent from Node.js MCP server to C# bridge via Named Pipe.
/// </summary>
public class PipeRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    public T? GetParams<T>()
    {
        if (Params == null || Params.Value.ValueKind == JsonValueKind.Undefined)
            return default;

        return JsonSerializer.Deserialize<T>(Params.Value.GetRawText(), PipeMessageSerializer.Options);
    }
}

/// <summary>
/// Response message sent from C# bridge back to Node.js MCP server.
/// </summary>
public class PipeResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PipeError? Error { get; set; }

    public static PipeResponse Success(string id, object? result = null) =>
        new() { Id = id, Result = result };

    public static PipeResponse Failure(string id, int code, string message) =>
        new() { Id = id, Error = new PipeError { Code = code, Message = message } };

    public static PipeResponse Failure(string id, int code, string message, object? data) =>
        new() { Id = id, Error = new PipeError { Code = code, Message = message, Data = data } };
}

/// <summary>
/// Error detail in a PipeResponse.
/// </summary>
public class PipeError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

/// <summary>
/// Standard error codes for the IPC protocol.
/// </summary>
public static class PipeErrorCodes
{
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int SolidWorksNotConnected = -32000;
    public const int SolidWorksOperationFailed = -32001;
}

/// <summary>
/// Shared JSON serializer options for pipe messages.
/// </summary>
public static class PipeMessageSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    static PipeMessageSerializer()
    {
        Options.Converters.Add(new JsonStringEnumConverter());
    }

    public static byte[] Serialize<T>(T obj) =>
        JsonSerializer.SerializeToUtf8Bytes(obj, Options);

    public static T? Deserialize<T>(ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize<T>(utf8Json, Options);

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);
}
