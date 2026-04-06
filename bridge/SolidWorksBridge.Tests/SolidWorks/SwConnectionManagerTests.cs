using Moq;
using System.Runtime.InteropServices;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

[Collection("SolidWorks Integration")]
public class SwConnectionManagerTests
{
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
        connector.Setup(c => c.GetActiveInstance()).Returns(mockApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();

        Assert.True(manager.IsConnected);
        Assert.Same(mockApp.Object, manager.SwApp);
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
        connector.Setup(c => c.GetActiveInstance()).Returns((ISldWorksApp?)null);
        connector.Setup(c => c.CreateNewInstance()).Returns(mockApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();

        Assert.True(manager.IsConnected);
        Assert.Same(mockApp.Object, manager.SwApp);
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

}
