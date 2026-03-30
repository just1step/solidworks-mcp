using Moq;
using SolidWorks.Interop.sldworks;
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
        var feat = FakeFeature("Boss-Extrude1");
        doc.Setup(d => d.FeatureBoss2(
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()));
        doc.Setup(d => d.IFeatureByPositionReverse(0)).Returns(feat);

        var info = new FeatureService(manager.Object).Extrude(0.01);

        Assert.Equal("Boss-Extrude1", info.Name);
        Assert.Equal("Extrude", info.Type);
    }

    [Fact]
    public void Extrude_NullReturnFromCom_Throws()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        doc.Setup(d => d.IFeatureByPositionReverse(0)).Returns((Feature?)null);

        Assert.Throws<InvalidOperationException>(() =>
            new FeatureService(manager.Object).Extrude(0.01));
    }

    [Fact]
    public void Extrude_CallsEnsureConnected()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        doc.Setup(d => d.IFeatureByPositionReverse(0)).Returns(FakeFeature());

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
        doc.Setup(d => d.IFeatureByPositionReverse(0)).Returns(FakeFeature());

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
        var (manager, _, _, doc) = ConnectedWithFm();
        doc.Setup(d => d.FeatureCut2(
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>()));
        doc.Setup(d => d.IFeatureByPositionReverse(0)).Returns(FakeFeature("Cut-Extrude1"));

        var info = new FeatureService(manager.Object).ExtrudeCut(0.01);

        Assert.Equal("Cut-Extrude1", info.Name);
        Assert.Equal("ExtrudeCut", info.Type);
    }

    [Fact]
    public void ExtrudeCut_NullReturnFromCom_Throws()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        doc.Setup(d => d.IFeatureByPositionReverse(0)).Returns((Feature?)null);

        Assert.Throws<InvalidOperationException>(() =>
            new FeatureService(manager.Object).ExtrudeCut(0.01));
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

    // ─────────────────────────────────────────────────────────────
    // SimpleHole
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void SimpleHole_ReturnsFeatureInfo_WithSimpleHoleType()
    {
        var (manager, _, _, doc) = ConnectedWithFm();
        doc.Setup(d => d.SimpleHole3(
                It.IsAny<double>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()));
        doc.Setup(d => d.IFeatureByPositionReverse(0)).Returns(FakeFeature("Hole1"));

        var info = new FeatureService(manager.Object).SimpleHole(0.01, 0.02);

        Assert.Equal("Hole1", info.Name);
        Assert.Equal("SimpleHole", info.Type);
    }
}
