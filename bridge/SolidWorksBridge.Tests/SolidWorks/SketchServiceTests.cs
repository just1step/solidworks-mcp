using Moq;
using SolidWorks.Interop.sldworks;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

public class SketchServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Connected manager whose SwApp.SketchManager returns a mock ISketchManager.
    /// </summary>
    private static (Mock<ISwConnectionManager> manager,
                    Mock<ISldWorksApp> swApp,
                    Mock<ISketchManager> skm)
        ConnectedWithSketchMgr()
    {
        var skm = new Mock<ISketchManager>();
        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.SketchManager).Returns(skm.Object);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        return (manager, swApp, skm);
    }

    /// <summary>
    /// Connected manager whose SwApp.SketchManager returns null (no open document).
    /// </summary>
    private static Mock<ISwConnectionManager> ConnectedNoDoc()
    {
        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.SketchManager).Returns((ISketchManager?)null);

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
        Assert.Throws<ArgumentNullException>(() => new SketchService(null!));
    }

    // ─────────────────────────────────────────────────────────────
    // InsertSketch / FinishSketch
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void InsertSketch_CallsInsertSketchTrue()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        var svc = new SketchService(manager.Object);

        svc.InsertSketch();

        skm.Verify(s => s.InsertSketch(true), Times.Once);
    }

    [Fact]
    public void FinishSketch_CallsInsertSketchFalse()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        var svc = new SketchService(manager.Object);

        svc.FinishSketch();

        skm.Verify(s => s.InsertSketch(false), Times.Once);
    }

    [Fact]
    public void InsertSketch_NoActiveDocument_Throws()
    {
        var manager = ConnectedNoDoc();
        var svc = new SketchService(manager.Object);

        Assert.Throws<InvalidOperationException>(() => svc.InsertSketch());
    }

    [Fact]
    public void InsertSketch_CallsEnsureConnected()
    {
        var (manager, _, _) = ConnectedWithSketchMgr();
        new SketchService(manager.Object).InsertSketch();

        manager.Verify(m => m.EnsureConnected(), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // AddPoint
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddPoint_ReturnsPointInfo_WithCorrectCoordinates()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        var point = new Mock<SketchPoint>().Object;
        skm.Setup(s => s.CreatePoint(0.01, 0.02, 0)).Returns(point);

        var info = new SketchService(manager.Object).AddPoint(0.01, 0.02);

        Assert.Equal("Point", info.Type);
        Assert.Equal(0.01, info.X1);
        Assert.Equal(0.02, info.Y1);
        Assert.Equal(0.01, info.X2);
        Assert.Equal(0.02, info.Y2);
    }

    [Fact]
    public void AddPoint_NullReturnFromCom_Throws()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        skm.Setup(s => s.CreatePoint(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
            .Returns((SketchPoint?)null!);

        Assert.Throws<InvalidOperationException>(() => new SketchService(manager.Object).AddPoint(0, 0));
    }

    // ─────────────────────────────────────────────────────────────
    // AddEllipse
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddEllipse_ReturnsEllipseInfo_WithCorrectCoordinates()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        var seg = new Mock<SketchSegment>().Object;
        skm.Setup(s => s.CreateEllipse(0, 0, 0, 0.03, 0, 0, 0, 0.01, 0)).Returns(seg);

        var info = new SketchService(manager.Object).AddEllipse(0, 0, 0.03, 0, 0, 0.01);

        Assert.Equal("Ellipse", info.Type);
        Assert.Equal(0, info.X1);
        Assert.Equal(0, info.Y1);
        Assert.Equal(0.03, info.X2);
        Assert.Equal(0, info.Y2);
    }

    [Fact]
    public void AddEllipse_NullReturnFromCom_Throws()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        skm.Setup(s => s.CreateEllipse(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
            .Returns((SketchSegment?)null!);

        Assert.Throws<InvalidOperationException>(() => new SketchService(manager.Object).AddEllipse(0, 0, 0.03, 0, 0, 0.01));
    }

    // ─────────────────────────────────────────────────────────────
    // AddPolygon
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddPolygon_ReturnsPolygonInfo_WithCorrectCoordinates()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        skm.Setup(s => s.CreatePolygon(0, 0, 0, 0.02, 0, 0, 6, true)).Returns(new object());

        var info = new SketchService(manager.Object).AddPolygon(0, 0, 0.02, 0, 6, true);

        Assert.Equal("Polygon", info.Type);
        Assert.Equal(0, info.X1);
        Assert.Equal(0, info.Y1);
        Assert.Equal(0.02, info.X2);
        Assert.Equal(0, info.Y2);
    }

    [Fact]
    public void AddPolygon_NullReturnFromCom_Throws()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        skm.Setup(s => s.CreatePolygon(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<bool>()))
            .Returns((object?)null!);

        Assert.Throws<InvalidOperationException>(() => new SketchService(manager.Object).AddPolygon(0, 0, 0.02, 0, 6, true));
    }

    // ─────────────────────────────────────────────────────────────
    // AddText
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddText_ReturnsTextInfo_WithCorrectCoordinates()
    {
        var (manager, swApp, _) = ConnectedWithSketchMgr();
        var doc = new Mock<IModelDoc2>();
        swApp.Setup(s => s.IActiveDoc2).Returns(doc.Object);
        doc.Setup(d => d.InsertSketchText(0.01, 0.02, 0, "HELLO", 0, 0, 0, 100, 100))
            .Returns(new object());

        var info = new SketchService(manager.Object).AddText(0.01, 0.02, "HELLO");

        Assert.Equal("Text", info.Type);
        Assert.Equal(0.01, info.X1);
        Assert.Equal(0.02, info.Y1);
        Assert.Equal(0.01, info.X2);
        Assert.Equal(0.02, info.Y2);
    }

    [Fact]
    public void AddText_EmptyText_Throws()
    {
        var (manager, _, _) = ConnectedWithSketchMgr();

        Assert.Throws<ArgumentException>(() => new SketchService(manager.Object).AddText(0, 0, ""));
    }

    [Fact]
    public void AddText_NullReturnFromCom_Throws()
    {
        var (manager, swApp, _) = ConnectedWithSketchMgr();
        var doc = new Mock<IModelDoc2>();
        swApp.Setup(s => s.IActiveDoc2).Returns(doc.Object);
        doc.Setup(d => d.InsertSketchText(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((object?)null!);

        Assert.Throws<InvalidOperationException>(() => new SketchService(manager.Object).AddText(0, 0, "HELLO"));
    }

    // ─────────────────────────────────────────────────────────────
    // AddLine
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddLine_ReturnsLineInfo_WithCorrectCoordinates()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        var seg = new Mock<SketchSegment>().Object;
        skm.Setup(s => s.CreateLine(0.01, 0.02, 0, 0.05, 0.06, 0)).Returns(seg);

        var svc = new SketchService(manager.Object);
        var info = svc.AddLine(0.01, 0.02, 0.05, 0.06);

        Assert.Equal("Line", info.Type);
        Assert.Equal(0.01, info.X1);
        Assert.Equal(0.02, info.Y1);
        Assert.Equal(0.05, info.X2);
        Assert.Equal(0.06, info.Y2);
    }

    [Fact]
    public void AddLine_NullReturnFromCom_Throws()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        skm.Setup(s => s.CreateLine(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
           .Returns((SketchSegment?)null!);

        var svc = new SketchService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.AddLine(0, 0, 0.01, 0.01));
    }

    // ─────────────────────────────────────────────────────────────
    // AddCircle
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddCircle_ReturnsCircleInfo_WithCorrectCoordinates()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        var seg = new Mock<SketchSegment>().Object;
        skm.Setup(s => s.CreateCircleByRadius(0.01, 0.02, 0, 0.005)).Returns(seg);

        var info = new SketchService(manager.Object).AddCircle(0.01, 0.02, 0.005);

        Assert.Equal("Circle", info.Type);
        Assert.Equal(0.01, info.X1);  // center x
        Assert.Equal(0.02, info.Y1);  // center y
        Assert.Equal(0.015, info.X2, precision: 10); // cx + radius
        Assert.Equal(0.02, info.Y2);  // cy
    }

    [Fact]
    public void AddCircle_NullReturnFromCom_Throws()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        skm.Setup(s => s.CreateCircleByRadius(It.IsAny<double>(), It.IsAny<double>(),
                                              It.IsAny<double>(), It.IsAny<double>()))
           .Returns((SketchSegment?)null!);

        Assert.Throws<InvalidOperationException>(() => new SketchService(manager.Object).AddCircle(0, 0, 0.01));
    }

    // ─────────────────────────────────────────────────────────────
    // AddRectangle
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddRectangle_ReturnsRectangleInfo_WithCorrectCoordinates()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        skm.Setup(s => s.CreateCornerRectangle(
                -0.05, -0.03, 0, 0.05, 0.03, 0))
           .Returns(new object());

        var info = new SketchService(manager.Object).AddRectangle(-0.05, -0.03, 0.05, 0.03);

        Assert.Equal("Rectangle", info.Type);
        Assert.Equal(-0.05, info.X1);
        Assert.Equal(-0.03, info.Y1);
        Assert.Equal(0.05, info.X2);
        Assert.Equal(0.03, info.Y2);
    }

    [Fact]
    public void AddRectangle_NullReturnFromCom_Throws()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        skm.Setup(s => s.CreateCornerRectangle(It.IsAny<double>(), It.IsAny<double>(),
                               It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
           .Returns((object?)null!);

        Assert.Throws<InvalidOperationException>(() => new SketchService(manager.Object).AddRectangle(0, 0, 0.1, 0.1));
    }

    // ─────────────────────────────────────────────────────────────
    // AddArc
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddArc_ReturnsArcInfo_WithCorrectCoordinates()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        var seg = new Mock<SketchSegment>().Object;
        skm.Setup(s => s.CreateArc(0, 0, 0, 0.01, 0, 0, 0, 0.01, 0, (short)1)).Returns(seg);

        var info = new SketchService(manager.Object).AddArc(0, 0, 0.01, 0, 0, 0.01, 1);

        Assert.Equal("Arc", info.Type);
        Assert.Equal(0, info.X1);   // center x
        Assert.Equal(0, info.Y1);   // center y
        Assert.Equal(0, info.X2);   // end x
        Assert.Equal(0.01, info.Y2); // end y
    }

    [Fact]
    public void AddArc_NullReturnFromCom_Throws()
    {
        var (manager, _, skm) = ConnectedWithSketchMgr();
        skm.Setup(s => s.CreateArc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                                   It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                                   It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                                   It.IsAny<short>()))
           .Returns((SketchSegment?)null!);

        Assert.Throws<InvalidOperationException>(() =>
            new SketchService(manager.Object).AddArc(0, 0, 0.01, 0, 0, 0.01, 1));
    }
}
