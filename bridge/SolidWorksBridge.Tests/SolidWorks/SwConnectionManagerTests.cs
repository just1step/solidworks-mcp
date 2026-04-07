using Moq;
using System.Runtime.InteropServices;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

[Collection("SolidWorks Integration")]
public class SwConnectionManagerTests
{
    private static Mock<ISldWorksApp> VersionedApp(string revisionNumber)
    {
        var app = new Mock<ISldWorksApp>();
        app.Setup(a => a.GetDocumentCount()).Returns(0);
        app.Setup(a => a.GetRevisionNumber()).Returns(revisionNumber);
        app.Setup(a => a.GetBuildNumbers()).Returns(new SwBuildNumbers(revisionNumber.Split('.')[0], revisionNumber, string.Empty));
        app.Setup(a => a.GetExecutablePath()).Returns(@"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\sldworks.exe");
        app.Setup(a => a.GetCurrentLicenseType()).Returns(0);
        return app;
    }

    // ─────────────────────────────────────────────
    // Unit Tests (Mocked — no real SolidWorks needed)
    // ─────────────────────────────────────────────

    [Fact]
    public void IsConnected_InitiallyFalse()
    {
        var connector = new Mock<ISwComConnector>();
        var manager = new SwConnectionManager(connector.Object);

        Assert.False(manager.IsConnected);
        Assert.Null(manager.SwApp);
    }

    [Fact]
    public void Constructor_NullConnector_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SwConnectionManager(null!));
    }

    [Fact]
    public void Connect_ActiveInstanceExists_UsesItAndSetsVisible()
    {
        var mockApp = new Mock<ISldWorksApp>();
        mockApp.Setup(a => a.GetDocumentCount()).Returns(0);
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.HasRunningProcess()).Returns(true);
        connector.SetupGet(c => c.LastResolvedProgId).Returns("SldWorks.Application.32");
        connector.Setup(c => c.GetActiveInstance()).Returns(mockApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();

        Assert.True(manager.IsConnected);
        Assert.Same(mockApp.Object, manager.SwApp);
        Assert.NotNull(manager.LastConnectionAttempt);
        Assert.Equal("running-process", manager.LastConnectionAttempt!.ConnectionSource);
        // Verify Visible = true was actually set on the SW app
        mockApp.VerifySet(a => a.Visible = true, Times.Once);
        // Verify CreateNewInstance was never called
        connector.Verify(c => c.CreateNewInstance(), Times.Never);
    }

    [Fact]
    public void Connect_NoActiveInstance_CreatesNewAndSetsVisible()
    {
        var mockApp = new Mock<ISldWorksApp>();
        mockApp.Setup(a => a.GetDocumentCount()).Returns(0);
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.HasRunningProcess()).Returns(false);
        connector.SetupGet(c => c.LastResolvedProgId).Returns("SldWorks.Application.32");
        connector.Setup(c => c.GetActiveInstance()).Returns((ISldWorksApp?)null);
        connector.Setup(c => c.CreateNewInstance()).Returns(mockApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();

        Assert.True(manager.IsConnected);
        Assert.Same(mockApp.Object, manager.SwApp);
        Assert.NotNull(manager.LastConnectionAttempt);
        Assert.Equal("new-instance", manager.LastConnectionAttempt!.ConnectionSource);
        mockApp.VerifySet(a => a.Visible = true, Times.Once);
        connector.Verify(c => c.CreateNewInstance(), Times.Once);
    }

    [Fact]
    public void Connect_AlreadyConnected_DoesNotReconnect()
    {
        var mockApp = new Mock<ISldWorksApp>();
        mockApp.Setup(a => a.GetDocumentCount()).Returns(0);
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(mockApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();
        manager.Connect(); // second call should be no-op

        // GetActiveInstance called exactly once, not twice
        connector.Verify(c => c.GetActiveInstance(), Times.Once);
    }

    [Fact]
    public void Disconnect_SetsNotConnected()
    {
        var mockApp = new Mock<ISldWorksApp>();
        mockApp.Setup(a => a.GetDocumentCount()).Returns(0);
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(mockApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();
        Assert.True(manager.IsConnected);

        manager.Disconnect();

        Assert.False(manager.IsConnected);
        Assert.Null(manager.SwApp);
    }

    [Fact]
    public void EnsureConnected_WhenConnected_DoesNotThrow()
    {
        var mockApp = new Mock<ISldWorksApp>();
        mockApp.Setup(a => a.GetDocumentCount()).Returns(0);
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(mockApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();

        // Should not throw
        var ex = Record.Exception(() => manager.EnsureConnected());
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureConnected_WhenNotConnected_Connects()
    {
        var mockApp = new Mock<ISldWorksApp>();
        mockApp.Setup(a => a.GetDocumentCount()).Returns(0);
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns((ISldWorksApp?)null);
        connector.Setup(c => c.CreateNewInstance()).Returns(mockApp.Object);
        var manager = new SwConnectionManager(connector.Object);

        var ex = Record.Exception(() => manager.EnsureConnected());

        Assert.Null(ex);
        Assert.Same(mockApp.Object, manager.SwApp);
        connector.Verify(c => c.CreateNewInstance(), Times.Once);
    }

    [Fact]
    public void EnsureConnected_WhenConnectionCreationFails_Throws()
    {
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns((ISldWorksApp?)null);
        connector.Setup(c => c.CreateNewInstance()).Throws(new InvalidOperationException("Failed to create SolidWorks instance"));

        var manager = new SwConnectionManager(connector.Object);

        Assert.Throws<InvalidOperationException>(() => manager.EnsureConnected());
    }

    [Fact]
    public void Disconnect_ThenReconnect_WorksCorrectly()
    {
        var mockApp = new Mock<ISldWorksApp>();
        mockApp.Setup(a => a.GetDocumentCount()).Returns(0);
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(mockApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();
        manager.Disconnect();

        Assert.False(manager.IsConnected);

        manager.Connect(); // reconnect

        Assert.True(manager.IsConnected);
        // GetActiveInstance called twice: once on first Connect, once on reconnect
        connector.Verify(c => c.GetActiveInstance(), Times.Exactly(2));
    }

    [Fact]
    public void Connect_WhenCachedSessionIsStale_ReattachesToRunningInstance()
    {
        var staleApp = new Mock<ISldWorksApp>();
        staleApp.SetupSequence(a => a.GetDocumentCount())
            .Returns(0)
            .Throws(new COMException("RPC server unavailable", unchecked((int)0x800706BA)));

        var refreshedApp = new Mock<ISldWorksApp>();
        refreshedApp.Setup(a => a.GetDocumentCount()).Returns(0);

        var connector = new Mock<ISwComConnector>();
        connector.SetupSequence(c => c.GetActiveInstance())
            .Returns(staleApp.Object)
            .Returns(refreshedApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();

        manager.Connect();

        Assert.Same(refreshedApp.Object, manager.SwApp);
        connector.Verify(c => c.CreateNewInstance(), Times.Never);
        refreshedApp.VerifySet(a => a.Visible = true, Times.Once);
    }

    [Fact]
    public void EnsureConnected_WhenCachedSessionIsStale_RecreatesSession()
    {
        var staleApp = new Mock<ISldWorksApp>();
        staleApp.SetupSequence(a => a.GetDocumentCount())
            .Returns(0)
            .Throws(new COMException("RPC server unavailable", unchecked((int)0x800706BA)));

        var recreatedApp = new Mock<ISldWorksApp>();
        recreatedApp.Setup(a => a.GetDocumentCount()).Returns(0);

        var connector = new Mock<ISwComConnector>();
        connector.SetupSequence(c => c.GetActiveInstance())
            .Returns(staleApp.Object)
            .Returns((ISldWorksApp?)null);
        connector.Setup(c => c.CreateNewInstance()).Returns(recreatedApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();

        var ex = Record.Exception(() => manager.EnsureConnected());

        Assert.Null(ex);
        Assert.Same(recreatedApp.Object, manager.SwApp);
        connector.Verify(c => c.CreateNewInstance(), Times.Once);
        recreatedApp.VerifySet(a => a.Visible = true, Times.Once);
    }

    [Fact]
    public void GetCompatibilityInfo_OnInteropBaseline_ReturnsCertifiedBaseline()
    {
        var app = VersionedApp("32.0.0");
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(app.Object);

        var manager = new SwConnectionManager(connector.Object);

        var result = manager.GetCompatibilityInfo();

        Assert.Equal("certified-baseline", result.CompatibilityState);
        Assert.NotNull(result.ConnectionVersionCheck);
        Assert.Equal("supported-2024-baseline", result.ConnectionVersionCheck!.Status);
        Assert.True(result.ConnectionVersionCheck.IsSupportedBaseline);
        Assert.Equal("32.1.0", result.InteropVersion);
        Assert.Equal(32, result.InteropRevisionMajor);
        Assert.Equal(2024, result.InteropMarketingYear);
        Assert.Equal("32.0.0", result.RuntimeVersion.RevisionNumber);
        Assert.Equal(32, result.RuntimeVersion.RevisionMajor);
        Assert.Equal(2024, result.RuntimeVersion.MarketingYear);
        Assert.Equal("swLicenseType_Full", result.License.Name);
        Assert.Equal(0, result.License.Value);
        Assert.Equal("certified", result.RuntimeSupport.ProductSupportLevel);
        Assert.Contains(result.RuntimeSupport.CapabilitySupport, entry =>
            entry.CapabilityId == SolidWorksSupportMatrix.HighRiskMutationWorkflowsCapability
            && entry.SupportLevel == "certified");
    }

    [Fact]
    public void GetCompatibilityInfo_OnNextMajor_ReturnsPlannedNextVersion()
    {
        var app = VersionedApp("33.0.0");
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(app.Object);

        var manager = new SwConnectionManager(connector.Object);

        var result = manager.GetCompatibilityInfo();

        Assert.Equal("planned-next-version", result.CompatibilityState);
        Assert.NotNull(result.ConnectionVersionCheck);
        Assert.Equal("under-development-2025-and-newer", result.ConnectionVersionCheck!.Status);
        Assert.False(result.ConnectionVersionCheck.IsSupportedBaseline);
        Assert.Equal(33, result.RuntimeVersion.RevisionMajor);
        Assert.Equal(2025, result.RuntimeVersion.MarketingYear);
        Assert.Equal("swLicenseType_Full", result.License.Name);
        Assert.Equal("targeted", result.RuntimeSupport.ProductSupportLevel);
        Assert.Contains(result.RuntimeSupport.CapabilitySupport, entry =>
            entry.CapabilityId == SolidWorksSupportMatrix.HighRiskMutationWorkflowsCapability
            && entry.SupportLevel == "targeted");
        Assert.Contains(result.Notices, notice => notice.Contains("planned certification window"));
    }

    [Fact]
    public void GetCompatibilityInfo_OnExperimentalDiscoveryVersion_ReturnsExperimentalRuntimeSupport()
    {
        var app = VersionedApp("34.0.0");
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(app.Object);

        var manager = new SwConnectionManager(connector.Object);

        var result = manager.GetCompatibilityInfo();

        Assert.Equal("unsupported-newer-version", result.CompatibilityState);
        Assert.NotNull(result.ConnectionVersionCheck);
        Assert.Equal("under-development-2025-and-newer", result.ConnectionVersionCheck!.Status);
        Assert.Equal(34, result.RuntimeVersion.RevisionMajor);
        Assert.Equal(2026, result.RuntimeVersion.MarketingYear);
        Assert.Equal("experimental", result.RuntimeSupport.ProductSupportLevel);
        Assert.Contains(result.RuntimeSupport.CapabilitySupport, entry =>
            entry.CapabilityId == SolidWorksSupportMatrix.ConnectionAndIntrospectionCapability
            && entry.SupportLevel == "experimental");
        Assert.Contains(result.RuntimeSupport.CapabilitySupport, entry =>
            entry.CapabilityId == SolidWorksSupportMatrix.HighRiskMutationWorkflowsCapability
            && entry.SupportLevel == "blocked");
    }

    [Fact]
    public void GetCompatibilityInfo_OnOlderMajor_ReturnsUnsupportedOlderVersion()
    {
        var app = VersionedApp("31.0.0");
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(app.Object);

        var manager = new SwConnectionManager(connector.Object);

        var result = manager.GetCompatibilityInfo();

        Assert.Equal("unsupported-older-version", result.CompatibilityState);
        Assert.NotNull(result.ConnectionVersionCheck);
        Assert.Equal("unsupported-before-2024", result.ConnectionVersionCheck!.Status);
        Assert.False(result.ConnectionVersionCheck.IsSupportedBaseline);
        Assert.Equal(31, result.RuntimeVersion.RevisionMajor);
        Assert.Equal(2023, result.RuntimeVersion.MarketingYear);
        Assert.Equal("swLicenseType_Full", result.License.Name);
        Assert.Contains(result.Notices, notice => notice.Contains("older than the compiled interop baseline"));
    }

    [Fact]
    public void GetCompatibilityInfo_OnPremiumLicense_DecodesLicenseName()
    {
        var app = VersionedApp("32.0.0");
        app.Setup(a => a.GetCurrentLicenseType()).Returns(7);
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(app.Object);

        var manager = new SwConnectionManager(connector.Object);

        var result = manager.GetCompatibilityInfo();

        Assert.Equal(7, result.License.Value);
        Assert.Equal("swLicenseType_Full_Premium", result.License.Name);
        Assert.Contains("Premium", result.License.Description);
    }

}
