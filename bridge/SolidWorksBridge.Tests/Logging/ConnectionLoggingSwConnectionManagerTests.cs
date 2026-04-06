using Moq;
using SolidWorksBridge.SolidWorks;
using SolidWorksMcpApp.Logging;

namespace SolidWorksBridge.Tests.Logging;

public class ConnectionLoggingSwConnectionManagerTests
{
    private static SolidWorksCompatibilityInfo CompatibilityInfo() =>
        new(
            "certified-baseline",
            "compatibility summary",
            "32.1.0",
            32,
            2024,
            new SolidWorksRuntimeVersionInfo(
                "32.0.0",
                32,
                0,
                0,
                2024,
                new SwBuildNumbers("32", "32.0.0", string.Empty),
                @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\sldworks.exe"),
            new SolidWorksLicenseInfo(7, "swLicenseType_Full_Premium", "SolidWorks Premium license."),
            ["notice"]);

    [Fact]
    public void Connect_LogsRuntimeVersionAndLicenseToTrayLogBuffer()
    {
        var connectionManager = new Mock<ISwConnectionManager>();
        connectionManager.SetupSequence(m => m.IsConnected)
            .Returns(false)
            .Returns(true)
            .Returns(true);
        connectionManager.Setup(m => m.Connect());
        connectionManager.Setup(m => m.GetCompatibilityInfo()).Returns(CompatibilityInfo());

        var selectionService = new Mock<ISelectionService>();
        selectionService.Setup(s => s.GetSolidWorksContext()).Returns(
            new SolidWorksContextInfo("chinese-simplified", []));

        int initialCount = ServerLogBuffer.GetSnapshot().Count;
        var wrapper = new ConnectionLoggingSwConnectionManager(connectionManager.Object, () => selectionService.Object);

        wrapper.Connect();

        var newEntries = ServerLogBuffer.GetSnapshot().Skip(initialCount).ToArray();
        Assert.Contains(newEntries, entry =>
            entry.Source == "COM"
            && entry.Message.Contains("SolidWorks connection after connect:")
            && entry.Message.Contains("\"RevisionNumber\":\"32.0.0\"")
            && entry.Message.Contains("\"Name\":\"swLicenseType_Full_Premium\"")
            && entry.Message.Contains("\"CompatibilityState\":\"certified-baseline\""));
    }

    [Fact]
    public void EnsureConnected_WhenItAutoConnects_LogsRuntimeVersionAndLicenseToTrayLogBuffer()
    {
        var connectionManager = new Mock<ISwConnectionManager>();
        connectionManager.SetupSequence(m => m.IsConnected)
            .Returns(false)
            .Returns(true)
            .Returns(true);
        connectionManager.Setup(m => m.EnsureConnected());
        connectionManager.Setup(m => m.GetCompatibilityInfo()).Returns(CompatibilityInfo());

        var selectionService = new Mock<ISelectionService>();
        selectionService.Setup(s => s.GetSolidWorksContext()).Returns(
            new SolidWorksContextInfo("chinese-simplified", []));

        int initialCount = ServerLogBuffer.GetSnapshot().Count;
        var wrapper = new ConnectionLoggingSwConnectionManager(connectionManager.Object, () => selectionService.Object);

        wrapper.EnsureConnected();

        var newEntries = ServerLogBuffer.GetSnapshot().Skip(initialCount).ToArray();
        Assert.Contains(newEntries, entry =>
            entry.Source == "COM"
            && entry.Message.Contains("EnsureConnected established the SolidWorks session."));
        Assert.Contains(newEntries, entry =>
            entry.Source == "COM"
            && entry.Message.Contains("SolidWorks connection after connect:")
            && entry.Message.Contains("\"RevisionNumber\":\"32.0.0\"")
            && entry.Message.Contains("\"Name\":\"swLicenseType_Full_Premium\""));
    }
}