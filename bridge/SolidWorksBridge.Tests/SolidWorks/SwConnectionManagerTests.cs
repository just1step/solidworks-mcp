using Moq;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

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
        var connector = new Mock<ISwComConnector>();
        connector.Setup(c => c.GetActiveInstance()).Returns(mockApp.Object);

        var manager = new SwConnectionManager(connector.Object);
        manager.Connect();

        // Should not throw
        var ex = Record.Exception(() => manager.EnsureConnected());
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureConnected_WhenNotConnected_Throws()
    {
        var connector = new Mock<ISwComConnector>();
        var manager = new SwConnectionManager(connector.Object);

        Assert.Throws<InvalidOperationException>(() => manager.EnsureConnected());
    }

    [Fact]
    public void Disconnect_ThenReconnect_WorksCorrectly()
    {
        var mockApp = new Mock<ISldWorksApp>();
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

    // ─────────────────────────────────────────────
    // Integration Tests (require real SolidWorks)
    // Run: dotnet test --filter "Category=Integration"
    // ─────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_Connect_AttachesToRunningSolidWorks()
    {
        // Requires: SolidWorks 2024 already open on this machine
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);

        manager.Connect();

        Assert.True(manager.IsConnected,
            "Expected to connect to running SolidWorks. Make sure SW is open.");
        Assert.NotNull(manager.SwApp);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_SwApp_IsVisible_AfterConnect()
    {
        // Requires: SolidWorks running
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);

        manager.Connect();
        manager.EnsureConnected();

        Assert.True(manager.SwApp!.Visible,
            "SolidWorks window should be visible after Connect()");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_GetDocumentCount_ReturnsNonNegative()
    {
        // Requires: SolidWorks running
        // Expected: returns 0 if no documents open, or >=1 if any are open
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);

        manager.Connect();

        var count = manager.SwApp!.GetDocumentCount();

        Assert.True(count >= 0,
            $"GetDocumentCount() returned {count}, expected >= 0");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_Disconnect_DoesNotCrash()
    {
        // Requires: SolidWorks running
        // Expected: Disconnect clears state without throwing, SW stays open
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);

        manager.Connect();
        var ex = Record.Exception(() => manager.Disconnect());

        Assert.Null(ex);
        Assert.False(manager.IsConnected);
    }
}
