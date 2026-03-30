using System.Text.Json;
using SolidWorksBridge.Models;

namespace SolidWorksBridge.Tests.Models;

public class PipeMessageTests
{
    // --- PipeRequest Tests ---

    [Fact]
    public void PipeRequest_Serialize_Roundtrip()
    {
        var request = new PipeRequest
        {
            Id = "req-001",
            Method = "sw_connect",
            Params = JsonSerializer.SerializeToElement(new { timeout = 5000 })
        };

        var json = JsonSerializer.Serialize(request, PipeMessageSerializer.Options);
        var deserialized = PipeMessageSerializer.Deserialize<PipeRequest>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("req-001", deserialized!.Id);
        Assert.Equal("sw_connect", deserialized.Method);
        Assert.NotNull(deserialized.Params);
    }

    [Fact]
    public void PipeRequest_GetParams_ReturnsTypedObject()
    {
        var request = new PipeRequest
        {
            Id = "req-002",
            Method = "sw_open_document",
            Params = JsonSerializer.SerializeToElement(new { filePath = @"C:\test.sldprt", readOnly = true })
        };

        var p = request.GetParams<OpenDocParams>();
        Assert.NotNull(p);
        Assert.Equal(@"C:\test.sldprt", p!.FilePath);
        Assert.True(p.ReadOnly);
    }

    [Fact]
    public void PipeRequest_GetParams_NullParams_ReturnsDefault()
    {
        var request = new PipeRequest
        {
            Id = "req-003",
            Method = "sw_connect",
            Params = null
        };

        var p = request.GetParams<OpenDocParams>();
        Assert.Null(p);
    }

    [Fact]
    public void PipeRequest_DefaultValues()
    {
        var request = new PipeRequest();
        Assert.Equal(string.Empty, request.Id);
        Assert.Equal(string.Empty, request.Method);
        Assert.Null(request.Params);
    }

    // --- PipeResponse Tests ---

    [Fact]
    public void PipeResponse_Success_Roundtrip()
    {
        var response = PipeResponse.Success("req-001", new { connected = true });
        var json = JsonSerializer.Serialize(response, PipeMessageSerializer.Options);
        var deserialized = PipeMessageSerializer.Deserialize<PipeResponse>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("req-001", deserialized!.Id);
        Assert.NotNull(deserialized.Result);
        Assert.Null(deserialized.Error);
    }

    [Fact]
    public void PipeResponse_Failure_Roundtrip()
    {
        var response = PipeResponse.Failure("req-002", PipeErrorCodes.MethodNotFound, "Unknown method: foo");
        var json = JsonSerializer.Serialize(response, PipeMessageSerializer.Options);
        var deserialized = PipeMessageSerializer.Deserialize<PipeResponse>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("req-002", deserialized!.Id);
        Assert.Null(deserialized.Result);
        Assert.NotNull(deserialized.Error);
        Assert.Equal(PipeErrorCodes.MethodNotFound, deserialized.Error!.Code);
        Assert.Equal("Unknown method: foo", deserialized.Error.Message);
    }

    [Fact]
    public void PipeResponse_Success_NullResult_OmitsResultInJson()
    {
        var response = PipeResponse.Success("req-003");
        var json = JsonSerializer.Serialize(response, PipeMessageSerializer.Options);

        // result should be omitted from JSON when null
        Assert.DoesNotContain("\"result\"", json);
    }

    [Fact]
    public void PipeResponse_Failure_OmitsResultInJson()
    {
        var response = PipeResponse.Failure("req-004", PipeErrorCodes.InternalError, "oops");
        var json = JsonSerializer.Serialize(response, PipeMessageSerializer.Options);

        Assert.DoesNotContain("\"result\"", json);
        Assert.Contains("\"error\"", json);
    }

    // --- PipeError Tests ---

    [Fact]
    public void PipeError_DefaultValues()
    {
        var error = new PipeError();
        Assert.Equal(0, error.Code);
        Assert.Equal(string.Empty, error.Message);
    }

    // --- PipeErrorCodes Tests ---

    [Fact]
    public void PipeErrorCodes_HaveExpectedValues()
    {
        Assert.Equal(-32601, PipeErrorCodes.MethodNotFound);
        Assert.Equal(-32602, PipeErrorCodes.InvalidParams);
        Assert.Equal(-32603, PipeErrorCodes.InternalError);
        Assert.Equal(-32000, PipeErrorCodes.SolidWorksNotConnected);
        Assert.Equal(-32001, PipeErrorCodes.SolidWorksOperationFailed);
    }

    // --- PipeMessageSerializer Tests ---

    [Fact]
    public void Serializer_Utf8Bytes_Roundtrip()
    {
        var response = PipeResponse.Success("req-005", new { value = 42 });
        var bytes = PipeMessageSerializer.Serialize(response);
        var deserialized = PipeMessageSerializer.Deserialize<PipeResponse>(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal("req-005", deserialized!.Id);
    }

    [Fact]
    public void Serializer_UseCamelCase()
    {
        var response = PipeResponse.Failure("req-006", 1, "test");
        var json = JsonSerializer.Serialize(response, PipeMessageSerializer.Options);

        // Properties should be camelCase
        Assert.Contains("\"id\"", json);
        Assert.Contains("\"error\"", json);
        Assert.Contains("\"code\"", json);
        Assert.Contains("\"message\"", json);
    }

    // --- Helper test model ---
    private class OpenDocParams
    {
        public string FilePath { get; set; } = string.Empty;
        public bool ReadOnly { get; set; }
    }
}
