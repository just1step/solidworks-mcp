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
        comp.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
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

    [Fact]
    public void ListComponentsRecursive_ReturnsNestedInstancesWithHierarchy()
    {
        var (manager, assy) = ConnectedWithAssy();

        var child1 = new Mock<Component2>();
        child1.Setup(c => c.Name2).Returns("NestedPart-1");
        child1.Setup(c => c.GetPathName()).Returns(@"C:\NestedPart.sldprt");
        child1.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var child2 = new Mock<Component2>();
        child2.Setup(c => c.Name2).Returns("NestedPart-2");
        child2.Setup(c => c.GetPathName()).Returns(@"C:\NestedPart.sldprt");
        child2.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var subAssembly = new Mock<Component2>();
        subAssembly.Setup(c => c.Name2).Returns("SubAsm-1");
        subAssembly.Setup(c => c.GetPathName()).Returns(@"C:\SubAsm.sldasm");
        subAssembly.Setup(c => c.GetChildren()).Returns(new object[] { child1.Object, child2.Object });

        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { subAssembly.Object });

        var list = new AssemblyService(manager.Object).ListComponentsRecursive();

        Assert.Equal(3, list.Count);
        Assert.Contains(list, c => c.Name == "SubAsm-1" && c.HierarchyPath == "SubAsm-1" && c.Depth == 0);
        Assert.Contains(list, c => c.Name == "NestedPart-1" && c.HierarchyPath == "SubAsm-1/NestedPart-1" && c.Depth == 1);
        Assert.Contains(list, c => c.Name == "NestedPart-2" && c.HierarchyPath == "SubAsm-1/NestedPart-2" && c.Depth == 1);
    }

    [Fact]
    public void ListComponentsRecursive_EmptyAssembly_ReturnsEmptyList()
    {
        var (manager, assy) = ConnectedWithAssy();
        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { });

        var list = new AssemblyService(manager.Object).ListComponentsRecursive();

        Assert.Empty(list);
    }

    [Fact]
    public void CheckInterference_ReturnsInterferingInstances()
    {
        var (manager, assy) = ConnectedWithAssy();
        var detectionManager = new Mock<InterferenceDetectionMgr>();
        var interference = new Mock<Interference>();

        var part1 = new Mock<Component2>();
        part1.Setup(c => c.Name2).Returns("Part1-1");
        part1.Setup(c => c.GetPathName()).Returns(@"C:\Part1.sldprt");
        part1.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var part2 = new Mock<Component2>();
        part2.Setup(c => c.Name2).Returns("Part2-1");
        part2.Setup(c => c.GetPathName()).Returns(@"C:\Part2.sldprt");
        part2.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { part1.Object, part2.Object });
        assy.SetupGet(a => a.InterferenceDetectionManager).Returns(detectionManager.Object);
        interference.Setup(i => i.Components).Returns(new object[] { part2.Object, part1.Object });
        interference.Setup(i => i.IsPossibleInterference).Returns(false);
        interference.Setup(i => i.GetComponentCount()).Returns(2);
        detectionManager.Setup(m => m.GetInterferences()).Returns(new object[] { interference.Object });

        var result = new AssemblyService(manager.Object).CheckInterference();

        Assert.True(result.HasInterference);
        Assert.False(result.TreatCoincidenceAsInterference);
        Assert.Equal(2, result.CheckedComponentCount);
        Assert.Equal(2, result.InterferingFaceCount);
        Assert.Equal(2, result.InterferingComponents.Count);
        detectionManager.Verify(m => m.Done(), Times.Once);
    }

    [Fact]
    public void CheckInterference_WithHierarchyFilter_ChecksSubset()
    {
        var (manager, assy) = ConnectedWithAssy();
        var detectionManager = new Mock<InterferenceDetectionMgr>();
        var interference = new Mock<Interference>();

        var child = new Mock<Component2>();
        child.Setup(c => c.Name2).Returns("NestedPart-1");
        child.Setup(c => c.GetPathName()).Returns(@"C:\NestedPart.sldprt");
        child.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var sibling = new Mock<Component2>();
        sibling.Setup(c => c.Name2).Returns("NestedPart-2");
        sibling.Setup(c => c.GetPathName()).Returns(@"C:\NestedPart2.sldprt");
        sibling.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var subAssembly = new Mock<Component2>();
        subAssembly.Setup(c => c.Name2).Returns("SubAsm-1");
        subAssembly.Setup(c => c.GetPathName()).Returns(@"C:\SubAsm.sldasm");
        subAssembly.Setup(c => c.GetChildren()).Returns(new object[] { child.Object, sibling.Object });

        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { subAssembly.Object });
        assy.SetupGet(a => a.InterferenceDetectionManager).Returns(detectionManager.Object);
        interference.Setup(i => i.Components).Returns(new object[] { child.Object, sibling.Object });
        interference.Setup(i => i.IsPossibleInterference).Returns(false);
        interference.Setup(i => i.GetComponentCount()).Returns(2);
        detectionManager.Setup(m => m.GetInterferences()).Returns(new object[] { interference.Object });

        var result = new AssemblyService(manager.Object).CheckInterference(
            ["SubAsm-1/NestedPart-1", "SubAsm-1/NestedPart-2"],
            treatCoincidenceAsInterference: true);

        Assert.True(result.HasInterference);
        Assert.True(result.TreatCoincidenceAsInterference);
        Assert.Equal(2, result.CheckedComponentCount);
        Assert.Equal(2, result.InterferingFaceCount);
        Assert.Equal(2, result.InterferingComponents.Count);
        detectionManager.VerifySet(m => m.TreatCoincidenceAsInterference = true, Times.Once);
    }

    [Fact]
    public void CheckInterference_WhenFilterDoesNotMatch_ExcludesInterference()
    {
        var (manager, assy) = ConnectedWithAssy();
        var detectionManager = new Mock<InterferenceDetectionMgr>();
        var interference = new Mock<Interference>();
        var part1 = new Mock<Component2>();
        part1.Setup(c => c.Name2).Returns("Part1-1");
        part1.Setup(c => c.GetPathName()).Returns(@"C:\Part1.sldprt");
        part1.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
        var part2 = new Mock<Component2>();
        part2.Setup(c => c.Name2).Returns("Part2-1");
        part2.Setup(c => c.GetPathName()).Returns(@"C:\Part2.sldprt");
        part2.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { part1.Object, part2.Object });
        assy.SetupGet(a => a.InterferenceDetectionManager).Returns(detectionManager.Object);
        interference.Setup(i => i.Components).Returns(new object[] { part1.Object, part2.Object });
        interference.Setup(i => i.IsPossibleInterference).Returns(false);
        interference.Setup(i => i.GetComponentCount()).Returns(2);
        detectionManager.Setup(m => m.GetInterferences()).Returns(new object[] { interference.Object });

        var result = new AssemblyService(manager.Object).CheckInterference(["Missing/Part-1"]);

        Assert.False(result.HasInterference);
        Assert.Empty(result.InterferingComponents);
    }

    [Fact]
    public void ReplaceComponent_ReplacesTopLevelComponent()
    {
        var (manager, assy) = ConnectedWithAssy();
        var doc = assy.As<IModelDoc2>();
        var selectionManager = new Mock<SelectionMgr>();
        var selectData = new Mock<SelectData>();

        var part = new Mock<Component2>();
        part.Setup(c => c.Name2).Returns("Pulley-1");
        part.Setup(c => c.GetPathName()).Returns(@"C:\OldPulley.sldprt");
        part.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
        part.Setup(c => c.Select4(false, selectData.Object, false)).Returns(true);

        doc.Setup(d => d.ISelectionManager).Returns(selectionManager.Object);
        selectionManager.Setup(m => m.CreateSelectData()).Returns(selectData.Object);
        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { part.Object });
        assy.Setup(a => a.ReplaceComponents2(@"C:\NewPulley.sldprt", "", false, 0, true)).Returns(true);

        var result = new AssemblyService(manager.Object).ReplaceComponent("Pulley-1", @"C:\NewPulley.sldprt");

        Assert.True(result.Success);
        Assert.Equal("Pulley-1", result.ReplacedHierarchyPath);
        Assert.Equal(@"C:\NewPulley.sldprt", result.ReplacementFilePath);
        doc.Verify(d => d.ClearSelection2(true), Times.Exactly(2));
    }

    [Fact]
    public void ReplaceComponent_WhenTargetIsNested_Throws()
    {
        var (manager, assy) = ConnectedWithAssy();

        var child = new Mock<Component2>();
        child.Setup(c => c.Name2).Returns("Pulley-1");
        child.Setup(c => c.GetPathName()).Returns(@"C:\OldPulley.sldprt");
        child.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var subAssembly = new Mock<Component2>();
        subAssembly.Setup(c => c.Name2).Returns("SubAsm-1");
        subAssembly.Setup(c => c.GetPathName()).Returns(@"C:\SubAsm.sldasm");
        subAssembly.Setup(c => c.GetChildren()).Returns(new object[] { child.Object });

        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { subAssembly.Object });

        Assert.Throws<SolidWorksApiException>(() =>
            new AssemblyService(manager.Object).ReplaceComponent("SubAsm-1/Pulley-1", @"C:\NewPulley.sldprt"));
    }

    [Fact]
    public void ReplaceComponent_WhenSelectionFails_Throws()
    {
        var (manager, assy) = ConnectedWithAssy();
        var doc = assy.As<IModelDoc2>();
        var selectionManager = new Mock<SelectionMgr>();
        var selectData = new Mock<SelectData>();

        var part = new Mock<Component2>();
        part.Setup(c => c.Name2).Returns("Pulley-1");
        part.Setup(c => c.GetPathName()).Returns(@"C:\OldPulley.sldprt");
        part.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
        part.Setup(c => c.Select4(false, selectData.Object, false)).Returns(false);

        doc.Setup(d => d.ISelectionManager).Returns(selectionManager.Object);
        selectionManager.Setup(m => m.CreateSelectData()).Returns(selectData.Object);
        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { part.Object });

        Assert.Throws<SolidWorksApiException>(() =>
            new AssemblyService(manager.Object).ReplaceComponent("Pulley-1", @"C:\NewPulley.sldprt"));
    }

}
