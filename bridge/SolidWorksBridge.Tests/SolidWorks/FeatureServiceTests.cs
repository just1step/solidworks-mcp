using Moq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

public class FeatureServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────

    private static (Mock<ISwConnectionManager> manager,
                    Mock<ISldWorksApp> swApp,
                    Mock<IFeatureManager> fm,
                    Mock<IModelDoc2> doc)
        ConnectedWithFm()
    {
        var fm = new Mock<IFeatureManager>();
        var doc = new Mock<IModelDoc2>();
        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.FeatureManager).Returns(fm.Object);
        swApp.Setup(s => s.IActiveDoc2).Returns(doc.Object);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        return (manager, swApp, fm, doc);
    }

    private sealed class FakeActiveSketch
    {
        private readonly string _name;

        public FakeActiveSketch(string name)
        {
            _name = name;
        }

        public string GetName()
        {
            return _name;
        }
    }

    private static Mock<ISwConnectionManager> ConnectedNoDoc()
    {
        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.FeatureManager).Returns((IFeatureManager?)null);
        swApp.Setup(s => s.IActiveDoc2).Returns((IModelDoc2?)null);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        return manager;
    }

    /// Returns a mock Feature (which is an interface in the interop DLL) with the given name.
    private static Feature FakeFeature(string name = "Boss-Extrude1")
    {
        var feat = new Mock<Feature>();
        feat.Setup(f => f.Name).Returns(name);
        feat.Setup(f => f.GetTypeName2()).Returns("BossExtrude");
        return feat.Object;
    }

    private static Feature FakeFeature(string name, string typeName)
    {
        var feat = new Mock<Feature>();
        feat.Setup(f => f.Name).Returns(name);
        feat.Setup(f => f.GetTypeName2()).Returns(typeName);
        return feat.Object;
    }

    // ─────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullManager_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FeatureService(null!));
    }

    // ─────────────────────────────────────────────────────────────
    // Extrude
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void Extrude_ReturnsFeatureInfo_WithCorrectType()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        var before = FakeFeature("Sketch2", "ProfileFeature");
        var feat = FakeFeature("Boss-Extrude1", "BossExtrude");
        doc.Setup(d => d.FeatureBoss2(
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()));
        doc.SetupSequence(d => d.IFeatureByPositionReverse(0))
            .Returns(before)
            .Returns(feat);

        var info = new FeatureService(manager.Object).Extrude(0.01);

        Assert.Equal("Boss-Extrude1", info.Name);
        Assert.Equal("Extrude", info.Type);
    }

    [Fact]
    public void Extrude_NullReturnFromCom_Throws()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        doc.SetupSequence(d => d.IFeatureByPositionReverse(0))
            .Returns((Feature?)null)
            .Returns((Feature?)null);

        Assert.Throws<SolidWorksApiException>(() =>
            new FeatureService(manager.Object).Extrude(0.01));
    }

    [Fact]
    public void Extrude_OpenActiveSketch_ThrowsBeforeCallingCom()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        var sketch = new Mock<ISketch>();
        var openContour = new Mock<ISketchContour>();
        openContour.Setup(c => c.IsClosed()).Returns(false);
        sketch.Setup(s => s.GetSketchSegments()).Returns(new object[] { new Mock<SketchSegment>().Object });
        sketch.Setup(s => s.GetSketchContourCount()).Returns(1);
        sketch.Setup(s => s.GetSketchRegionCount()).Returns(0);
        sketch.Setup(s => s.GetSketchContours()).Returns(new object[] { openContour.Object });
        doc.Setup(d => d.GetActiveSketch2()).Returns(sketch.Object);

        var error = Assert.Throws<SolidWorksApiException>(() =>
            new FeatureService(manager.Object).Extrude(0.01));

        Assert.Contains("contains open contours", error.Message);
        doc.Verify(d => d.FeatureBoss2(
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public void Extrude_CallsEnsureConnected()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        doc.SetupSequence(d => d.IFeatureByPositionReverse(0))
            .Returns(FakeFeature("Sketch2", "ProfileFeature"))
            .Returns(FakeFeature());

        new FeatureService(manager.Object).Extrude(0.01);

        manager.Verify(m => m.EnsureConnected(), Times.Once);
    }

    [Fact]
    public void Extrude_NoActiveDocument_Throws()
    {
        var manager = ConnectedNoDoc();
        Assert.Throws<InvalidOperationException>(() =>
            new FeatureService(manager.Object).Extrude(0.01));
    }

    [Fact]
    public void Extrude_FlipDirection_PassesFalseForDirToApi()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        doc.SetupSequence(d => d.IFeatureByPositionReverse(0))
            .Returns(FakeFeature("Sketch2", "ProfileFeature"))
            .Returns(FakeFeature());

        new FeatureService(manager.Object).Extrude(0.01, flipDirection: true);

        // When flipDirection=true: Flip=true, Dir=false
        doc.Verify(d => d.FeatureBoss2(
            true /*Sd*/, true /*Flip*/, false /*Dir*/,
            It.IsAny<int>(), It.IsAny<int>(),
            0.01, It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // ExtrudeCut
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void ExtrudeCut_ReturnsFeatureInfo_WithCorrectType()
    {
        var (manager, _, fm, doc) = ConnectedWithFm();
        doc.SetupSequence(d => d.IFeatureByPositionReverse(0))
            .Returns(FakeFeature("Sketch2", "ProfileFeature"))
            .Returns(FakeFeature("Cut-Extrude1", "Cut"));
        fm.Setup(f => f.FeatureCut4(
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
                        It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<double>(), It.IsAny<bool>(), It.IsAny<bool>()))
                    .Returns(FakeFeature("Cut-Extrude1", "Cut"));

        var info = new FeatureService(manager.Object).ExtrudeCut(0.01);

        Assert.Equal("Cut-Extrude1", info.Name);
        Assert.Equal("ExtrudeCut", info.Type);
    }

    [Fact]
    public void ExtrudeCut_NullReturnFromCom_Throws()
    {
            var (manager, _, fm, doc) = ConnectedWithFm();
            doc.SetupSequence(d => d.IFeatureByPositionReverse(0))
              .Returns(FakeFeature("Sketch2", "ProfileFeature"))
              .Returns(FakeFeature("Sketch2", "ProfileFeature"));
          fm.Setup(f => f.FeatureCut4(
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<double>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns((Feature?)null);

        Assert.Throws<SolidWorksApiException>(() =>
            new FeatureService(manager.Object).ExtrudeCut(0.01));
    }

    [Fact]
    public void ExtrudeCut_OpenActiveSketch_ThrowsBeforeCallingCom()
    {
        var (manager, _, fm, doc) = ConnectedWithFm();
        var sketch = new Mock<ISketch>();
        var openContour = new Mock<ISketchContour>();
        openContour.Setup(c => c.IsClosed()).Returns(false);
        sketch.Setup(s => s.GetSketchSegments()).Returns(new object[] { new Mock<SketchSegment>().Object });
        sketch.Setup(s => s.GetSketchContourCount()).Returns(1);
        sketch.Setup(s => s.GetSketchRegionCount()).Returns(0);
        sketch.Setup(s => s.GetSketchContours()).Returns(new object[] { openContour.Object });
        doc.Setup(d => d.GetActiveSketch2()).Returns(sketch.Object);

        var error = Assert.Throws<SolidWorksApiException>(() =>
            new FeatureService(manager.Object).ExtrudeCut(0.01));

        Assert.Contains("contains open contours", error.Message);
        fm.Verify(f => f.FeatureCut4(
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<double>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public void Extrude_ClosedContoursWithoutRegions_ThrowsBeforeCallingCom()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        var sketch = new Mock<ISketch>();
        var closedContour = new Mock<ISketchContour>();
        closedContour.Setup(c => c.IsClosed()).Returns(true);
        sketch.Setup(s => s.GetSketchSegments()).Returns(new object[] { new Mock<SketchSegment>().Object });
        sketch.Setup(s => s.GetSketchContourCount()).Returns(1);
        sketch.Setup(s => s.GetSketchRegionCount()).Returns(0);
        sketch.Setup(s => s.GetSketchContours()).Returns(new object[] { closedContour.Object });
        doc.Setup(d => d.GetActiveSketch2()).Returns(sketch.Object);

        var error = Assert.Throws<SolidWorksApiException>(() =>
            new FeatureService(manager.Object).Extrude(0.01));

        Assert.Contains("does not contain any valid sketch regions", error.Message);
        doc.Verify(d => d.FeatureBoss2(
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public void ExtrudeCut_ClosesActiveSketchBeforeCallingApi()
    {
        var (manager, swApp, fm, doc) = ConnectedWithFm();
        var sketchManager = new Mock<ISketchManager>();
        swApp.Setup(s => s.SketchManager).Returns(sketchManager.Object);
        doc.Setup(d => d.GetActiveSketch2()).Returns(new FakeActiveSketch("Sketch2"));
        doc.SetupSequence(d => d.IFeatureByPositionReverse(0))
            .Returns(FakeFeature("Sketch2", "ProfileFeature"))
            .Returns(FakeFeature("Cut-Extrude1", "Cut"));
        fm.Setup(f => f.FeatureCut4(
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<double>(), It.IsAny<bool>(), It.IsAny<bool>()))
          .Returns(FakeFeature("Cut-Extrude1", "Cut"));

        new FeatureService(manager.Object).ExtrudeCut(0.01);

        doc.Verify(d => d.ClearSelection2(true), Times.Once);
        sketchManager.Verify(s => s.InsertSketch(true), Times.Once);
        fm.Verify(f => f.FeatureCut4(
            true, It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(),
            0.01, It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            false, false, false, true, true, true, true, false,
            0, 0, false, false), Times.Once);
    }

    [Fact]
    public void ExtrudeCut_WhenApiLeavesTopFeatureUnchanged_ThrowsDetailedError()
    {
        var (manager, _, fm, doc) = ConnectedWithFm();
        var sketchFeature = FakeFeature("Sketch2", "ProfileFeature");
        doc.SetupSequence(d => d.IFeatureByPositionReverse(0))
            .Returns(sketchFeature)
            .Returns(sketchFeature);
        fm.Setup(f => f.FeatureCut4(
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<double>(), It.IsAny<bool>(), It.IsAny<bool>()))
          .Returns(sketchFeature);

        var error = Assert.Throws<SolidWorksApiException>(() =>
            new FeatureService(manager.Object).ExtrudeCut(0.01));

        Assert.Contains("did not create a new cut feature", error.Message);
        Assert.Contains("Sketch2", error.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // Revolve
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void Revolve_ReturnsFeatureInfo_WithRevolveType()
    {
        var (manager, _, fm, _) = ConnectedWithFm();
        fm.Setup(f => f.FeatureRevolve2(
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
          .Returns(FakeFeature("Revolve1"));

        var info = new FeatureService(manager.Object).Revolve(360);

        Assert.Equal("Revolve1", info.Name);
        Assert.Equal("Revolve", info.Type);
    }

    [Fact]
    public void Revolve_WhenCut_ReturnsCutType()
    {
        var (manager, _, fm, _) = ConnectedWithFm();
        fm.Setup(f => f.FeatureRevolve2(
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
          .Returns(FakeFeature("RevolveCut1"));

        var info = new FeatureService(manager.Object).Revolve(360, isCut: true);

        Assert.Equal("RevolveCut", info.Type);
    }

    // ─────────────────────────────────────────────────────────────
    // Fillet
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void Fillet_ReturnsFeatureInfo_WithFilletType()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        doc.Setup(d => d.FeatureFillet5(
            It.IsAny<int>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<object>(), It.IsAny<object>(), It.IsAny<object>()))
          .Returns(1);
        doc.Setup(d => d.IFeatureByPositionReverse(0)).Returns(FakeFeature("Fillet1"));

        var info = new FeatureService(manager.Object).Fillet(0.003);

        Assert.Equal("Fillet1", info.Name);
        Assert.Equal("Fillet", info.Type);
    }

    [Fact]
    public void Fillet_NullReturnFromCom_Throws()
    {
                var (manager, _, _, doc) = ConnectedWithFm();
                doc.Setup(d => d.FeatureFillet5(
                                It.IsAny<int>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<int>(),
                                It.IsAny<object>(), It.IsAny<object>(), It.IsAny<object>()))
                    .Returns(0);
                doc.Setup(d => d.IFeatureByPositionReverse(0)).Returns((Feature?)null);

        Assert.Throws<InvalidOperationException>(() =>
            new FeatureService(manager.Object).Fillet(0.003));
    }

    // ─────────────────────────────────────────────────────────────
    // Chamfer
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void Chamfer_ReturnsFeatureInfo_WithChamferType()
    {
        var (manager, _, fm, _) = ConnectedWithFm();
        fm.Setup(f => f.InsertFeatureChamfer(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>()))
          .Returns(FakeFeature("Chamfer1"));

        var info = new FeatureService(manager.Object).Chamfer(0.002);

        Assert.Equal("Chamfer1", info.Name);
        Assert.Equal("Chamfer", info.Type);
    }

}
