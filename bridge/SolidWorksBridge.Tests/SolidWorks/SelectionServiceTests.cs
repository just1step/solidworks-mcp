using Moq;
using SolidWorks.Interop.sldworks;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

public class SelectionServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Connected manager whose SwApp.IActiveDoc2 returns the given mock doc.
    /// </summary>
    private static (Mock<ISwConnectionManager> manager,
                    Mock<ISldWorksApp> swApp,
                    Mock<IModelDoc2> doc)
        ConnectedWithDoc()
    {
        var doc = new Mock<IModelDoc2>();
        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.IActiveDoc2).Returns(doc.Object);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected()); // no-op

        return (manager, swApp, doc);
    }

    /// <summary>
    /// Connected manager whose SwApp.IActiveDoc2 returns null (no open document).
    /// </summary>
    private static Mock<ISwConnectionManager> ConnectedNoDoc()
    {
        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.IActiveDoc2).Returns((IModelDoc2?)null);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        return manager;
    }

    // ─────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullConnectionManager_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SelectionService(null!));
    }

    // ─────────────────────────────────────────────────────────────
    // SelectByName
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void SelectByName_Success_ReturnsSuccessResult()
    {
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.SelectByID("Front Plane", "PLANE", 0, 0, 0)).Returns(true);

        var svc = new SelectionService(manager.Object);
        var result = svc.SelectByName("Front Plane", "PLANE");

        Assert.True(result.Success);
        Assert.Contains("Front Plane", result.Message);
        doc.Verify(d => d.SelectByID("Front Plane", "PLANE", 0, 0, 0), Times.Once);
    }

    [Fact]
    public void SelectByName_Failure_ReturnsFailureResult()
    {
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.SelectByID("NonExistent", "PLANE", 0, 0, 0)).Returns(false);

        var svc = new SelectionService(manager.Object);
        var result = svc.SelectByName("NonExistent", "PLANE");

        Assert.False(result.Success);
        Assert.Contains("NonExistent", result.Message);
    }

    [Fact]
    public void SelectByName_NoActiveDocument_ThrowsInvalidOperation()
    {
        var manager = ConnectedNoDoc();
        var svc = new SelectionService(manager.Object);

        Assert.Throws<InvalidOperationException>(() => svc.SelectByName("Front Plane", "PLANE"));
    }

    [Fact]
    public void SelectByName_CallsEnsureConnected()
    {
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.SelectByID(It.IsAny<string>(), It.IsAny<string>(), 0, 0, 0)).Returns(true);

        var svc = new SelectionService(manager.Object);
        svc.SelectByName("Front Plane", "PLANE");

        manager.Verify(m => m.EnsureConnected(), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // ClearSelection
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void ClearSelection_CallsClearSelection2_WithTrue()
    {
        var (manager, _, doc) = ConnectedWithDoc();

        var svc = new SelectionService(manager.Object);
        svc.ClearSelection();

        doc.Verify(d => d.ClearSelection2(true), Times.Once);
    }

    [Fact]
    public void ClearSelection_NoActiveDocument_ThrowsInvalidOperation()
    {
        var manager = ConnectedNoDoc();
        var svc = new SelectionService(manager.Object);

        Assert.Throws<InvalidOperationException>(() => svc.ClearSelection());
    }

    [Fact]
    public void ClearSelection_CallsEnsureConnected()
    {
        var (manager, _, _) = ConnectedWithDoc();
        var svc = new SelectionService(manager.Object);
        svc.ClearSelection();

        manager.Verify(m => m.EnsureConnected(), Times.Once);
    }
}
