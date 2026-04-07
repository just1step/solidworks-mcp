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
            ["notice"],
            ConnectionVersionCheck: new SolidWorksConnectionVersionCheck(
                "supported-2024-baseline",
                "Only SolidWorks 2024 is fully supported for MCP connection in this bridge build.",
                true));

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
        connectionManager.SetupGet(m => m.LastConnectionAttempt).Returns(new SolidWorksConnectionAttemptInfo(
            "running-process",
            true,
            "SldWorks.Application.32",
            "Attached to a running SolidWorks process via COM."));

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
        Assert.Contains(newEntries, entry =>
            entry.Source == "COM"
            && entry.Message.Contains("SolidWorks connect path:")
            && entry.Message.Contains("\"ConnectionSource\":\"running-process\""));
        Assert.Contains(newEntries, entry =>
            entry.Source == "COM"
            && entry.Message.Contains("SolidWorks connection version check:")
            && entry.Message.Contains("supported-2024-baseline"));
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
        connectionManager.SetupGet(m => m.LastConnectionAttempt).Returns(new SolidWorksConnectionAttemptInfo(
            "new-instance",
            false,
            "SldWorks.Application.32",
            "Launched a new SolidWorks instance from the newest registered COM ProgID."));

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
        Assert.Contains(newEntries, entry =>
            entry.Source == "COM"
            && entry.Message.Contains("SolidWorks connect path:")
            && entry.Message.Contains("\"ConnectionSource\":\"new-instance\""));
    }
}