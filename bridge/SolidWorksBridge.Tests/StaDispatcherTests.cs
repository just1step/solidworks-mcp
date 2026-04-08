using ModelContextProtocol;
using SolidWorksMcpApp;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests;

public class StaDispatcherTests
{
    [Fact]
    public async Task InvokeLoggedAsync_WhenSolidWorksApiExceptionThrown_WrapsAsMcpExceptionWithOriginalMessage()
    {
        using var sta = new StaDispatcher();

        var error = await Assert.ThrowsAsync<McpException>(() =>
            sta.InvokeLoggedAsync<object?>(
                "ExtrudeCut",
                new { depth = 0.01 },
                () => throw SolidWorksApiErrorFactory.FromValidationFailure(
                    "IFeatureManager.FeatureCut4",
                    "The active sketch contains open contours, so cut extrude cannot create a solid feature.")));

        Assert.Contains("open contours", error.Message);
        Assert.IsType<SolidWorksApiException>(error.InnerException);
    }

    [Fact]
    public async Task InvokeLoggedAsync_WhenArgumentExceptionThrown_WrapsAsMcpExceptionWithOriginalMessage()
    {
        using var sta = new StaDispatcher();

        var error = await Assert.ThrowsAsync<McpException>(() =>
            sta.InvokeLoggedAsync<object?>(
                "AddText",
                new { x = 0.0, y = 0.0 },
                () => throw new ArgumentException("text must not be empty", "text")));

        Assert.Contains("text must not be empty", error.Message);
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Fact]
    public async Task InvokeLoggedAsync_WhenGenericExceptionThrown_PreservesOriginalExceptionType()
    {
        using var sta = new StaDispatcher();

        var error = await Assert.ThrowsAsync<Exception>(() =>
            sta.InvokeLoggedAsync<object?>(
                "UnexpectedFailure",
                null,
                () => throw new Exception("top secret failure")));

        Assert.Equal("top secret failure", error.Message);
        Assert.IsNotType<McpException>(error);
    }
}
