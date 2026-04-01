using Moq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

public class AssemblyServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────

    private static (Mock<ISwConnectionManager> manager,
                    Mock<IAssemblyDoc> assy)
        ConnectedWithAssy()
    {
        var assy = new Mock<IAssemblyDoc>();

        // IModelDoc2 must also implement IAssemblyDoc  — use a mock that does both
        var doc = assy.As<IModelDoc2>();

        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.IActiveDoc2).Returns(doc.Object);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        return (manager, assy);
    }

    private static Mock<ISwConnectionManager> ConnectedNonAssy()
    {
        // Active doc exists but is NOT an assembly (plain IModelDoc2 only)
        var doc = new Mock<IModelDoc2>();

        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.IActiveDoc2).Returns(doc.Object);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        return manager;
    }

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

    private static Component2 FakeComponent(string name = "Part1-1", string path = @"C:\Part1.sldprt")
    {
        var comp = new Mock<Component2>();
        comp.Setup(c => c.Name2).Returns(name);
        comp.Setup(c => c.GetPathName()).Returns(path);
        return comp.Object;
    }

    private static Mate2 FakeMate()
    {
        return new Mock<Mate2>().Object;
    }

    // ─────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullManager_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AssemblyService(null!));
    }

    // ─────────────────────────────────────────────────────────────
    // InsertComponent
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void InsertComponent_ReturnsComponentInfo()
    {
        var (manager, assy) = ConnectedWithAssy();
        var comp = FakeComponent("Part1-1", @"C:\Part1.sldprt");
        assy.Setup(a => a.AddComponent5(
                @"C:\Part1.sldprt",
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                0.0, 0.0, 0.0))
            .Returns(comp);

        var info = new AssemblyService(manager.Object).InsertComponent(@"C:\Part1.sldprt");

        Assert.Equal("Part1-1", info.Name);
        Assert.Equal(@"C:\Part1.sldprt", info.Path);
    }

    [Fact]
    public void InsertComponent_WithPosition_PassesCoordinatesToApi()
    {
        var (manager, assy) = ConnectedWithAssy();
        var comp = FakeComponent();
        assy.Setup(a => a.AddComponent5(
                It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                0.1, 0.2, 0.3))
            .Returns(comp);

        new AssemblyService(manager.Object).InsertComponent(@"C:\Part1.sldprt", 0.1, 0.2, 0.3);

        assy.Verify(a => a.AddComponent5(
            @"C:\Part1.sldprt",
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
            0.1, 0.2, 0.3), Times.Once);
    }

    [Fact]
    public void InsertComponent_EmptyFilePath_Throws()
    {
        var (manager, _) = ConnectedWithAssy();
        Assert.Throws<ArgumentException>(() =>
            new AssemblyService(manager.Object).InsertComponent(""));
    }

    [Fact]
    public void InsertComponent_NullReturnFromApi_Throws()
    {
        var (manager, assy) = ConnectedWithAssy();
        assy.Setup(a => a.AddComponent5(
                It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
            .Returns((Component2)null!);

        Assert.Throws<InvalidOperationException>(() =>
            new AssemblyService(manager.Object).InsertComponent(@"C:\Part1.sldprt"));
    }

    [Fact]
    public void InsertComponent_NoActiveDoc_Throws()
    {
        var manager = ConnectedNoDoc();
        Assert.Throws<InvalidOperationException>(() =>
            new AssemblyService(manager.Object).InsertComponent(@"C:\Part1.sldprt"));
    }

    [Fact]
    public void InsertComponent_NotAssembly_Throws()
    {
        var manager = ConnectedNonAssy();
        Assert.Throws<InvalidOperationException>(() =>
            new AssemblyService(manager.Object).InsertComponent(@"C:\Part1.sldprt"));
    }

    // ─────────────────────────────────────────────────────────────
    // AddMate variants
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddMateCoincident_CallsAddMate5WithCoincidentType()
    {
        var (manager, assy) = ConnectedWithAssy();
        int errOut = 1;
        assy.Setup(a => a.AddMate5(
                (int)MateType.Coincident, It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(),
                out errOut))
            .Returns(FakeMate());

        new AssemblyService(manager.Object).AddMateCoincident();

        assy.Verify(a => a.AddMate5(
            (int)MateType.Coincident, It.IsAny<int>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(),
            out errOut), Times.Once);
    }

    [Fact]
    public void AddMateConcentric_CallsAddMate5WithConcentricType()
    {
        var (manager, assy) = ConnectedWithAssy();
        int errOut = 1;
        assy.Setup(a => a.AddMate5(
                (int)MateType.Concentric, It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(),
                out errOut))
            .Returns(FakeMate());

        new AssemblyService(manager.Object).AddMateConcentric();

        assy.Verify(a => a.AddMate5(
            (int)MateType.Concentric, It.IsAny<int>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(),
            out errOut), Times.Once);
    }

    [Fact]
    public void AddMateDistance_PassesDistanceToApi()
    {
        var (manager, assy) = ConnectedWithAssy();
        int errOut = 1;
        assy.Setup(a => a.AddMate5(
                (int)MateType.Distance, It.IsAny<int>(), It.IsAny<bool>(),
                0.05, It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(),
                out errOut))
            .Returns(FakeMate());

        new AssemblyService(manager.Object).AddMateDistance(0.05);

        assy.Verify(a => a.AddMate5(
            (int)MateType.Distance, It.IsAny<int>(), It.IsAny<bool>(),
            0.05, It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(),
            out errOut), Times.Once);
    }

    [Fact]
    public void AddMateAngle_ConvertsDegreesToRadians()
    {
        var (manager, assy) = ConnectedWithAssy();
        int errOut = 1;
        double expectedRad = 90.0 * Math.PI / 180.0;
        assy.Setup(a => a.AddMate5(
                (int)MateType.Angle, It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsInRange(expectedRad - 1e-9, expectedRad + 1e-9, Moq.Range.Inclusive),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(),
                out errOut))
            .Returns(FakeMate());

        new AssemblyService(manager.Object).AddMateAngle(90.0);

        assy.Verify(a => a.AddMate5(
            (int)MateType.Angle, It.IsAny<int>(), It.IsAny<bool>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsInRange(expectedRad - 1e-9, expectedRad + 1e-9, Moq.Range.Inclusive),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(),
            out errOut), Times.Once);
    }

    [Fact]
    public void AddMate_NullReturnFromApi_Throws()
    {
        var (manager, assy) = ConnectedWithAssy();
        int errOut = (int)swAddMateError_e.swAddMateError_IncorrectSelections;
        assy.Setup(a => a.AddMate5(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(),
                out errOut))
            .Returns((Mate2)null!);

        Assert.Throws<SolidWorksApiException>(() =>
            new AssemblyService(manager.Object).AddMateCoincident());
    }

    // ─────────────────────────────────────────────────────────────
    // ListComponents
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void ListComponents_ReturnsAllTopLevelComponents()
    {
        var (manager, assy) = ConnectedWithAssy();
        var comp1 = FakeComponent("Part1-1", @"C:\Part1.sldprt");
        var comp2 = FakeComponent("Part2-1", @"C:\Part2.sldprt");
        assy.Setup(a => a.GetComponents(true))
            .Returns(new object[] { comp1, comp2 });

        var list = new AssemblyService(manager.Object).ListComponents();

        Assert.Equal(2, list.Count);
        Assert.Contains(list, c => c.Name == "Part1-1");
        Assert.Contains(list, c => c.Name == "Part2-1");
    }

    [Fact]
    public void ListComponents_EmptyAssembly_ReturnsEmptyList()
    {
        var (manager, assy) = ConnectedWithAssy();
        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { });

        var list = new AssemblyService(manager.Object).ListComponents();

        Assert.Empty(list);
    }

    [Fact]
    public void ListComponents_NullFromApi_ReturnsEmptyList()
    {
        var (manager, assy) = ConnectedWithAssy();
        assy.Setup(a => a.GetComponents(true)).Returns((object)null!);

        var list = new AssemblyService(manager.Object).ListComponents();

        Assert.Empty(list);
    }
}
