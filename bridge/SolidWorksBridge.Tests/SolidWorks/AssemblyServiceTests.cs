using Moq;
using System.Runtime.InteropServices;
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

    private static string CreateTempModelFile(string extension = ".sldprt")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static void SetupToolsCheckInterference2(
        Mock<IAssemblyDoc> assy,
        int checkedComponentCount,
        bool treatCoincidenceAsInterference,
        object[] interferingComponents,
        object[] interferingFaces)
    {
        object outComponents = interferingComponents;
        object outFaces = interferingFaces;
        assy.Setup(a => a.ToolsCheckInterference2(
                checkedComponentCount,
                It.IsAny<object>(),
                treatCoincidenceAsInterference,
                out outComponents,
                out outFaces));
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
        var filePath = CreateTempModelFile();
        try
        {
            var comp = FakeComponent("Part1-1", filePath);
            assy.Setup(a => a.AddComponent5(
                    filePath,
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                    0.0, 0.0, 0.0))
                .Returns(comp);

            var info = new AssemblyService(manager.Object).InsertComponent(filePath);

            Assert.Equal("Part1-1", info.Name);
            Assert.Equal(filePath, info.Path);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void InsertComponent_WithPosition_PassesCoordinatesToApi()
    {
        var (manager, assy) = ConnectedWithAssy();
        var filePath = CreateTempModelFile();
        try
        {
            var comp = FakeComponent(path: filePath);
            assy.Setup(a => a.AddComponent5(
                    It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                    0.1, 0.2, 0.3))
                .Returns(comp);

            new AssemblyService(manager.Object).InsertComponent(filePath, 0.1, 0.2, 0.3);

            assy.Verify(a => a.AddComponent5(
                filePath,
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                0.1, 0.2, 0.3), Times.Once);
        }
        finally
        {
            File.Delete(filePath);
        }
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
        var filePath = CreateTempModelFile();
        try
        {
            assy.Setup(a => a.AddComponent5(
                    It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns((Component2)null!);

            Assert.Throws<InvalidOperationException>(() =>
                new AssemblyService(manager.Object).InsertComponent(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void InsertComponent_NoActiveDoc_Throws()
    {
        var manager = ConnectedNoDoc();
        var filePath = CreateTempModelFile();
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                new AssemblyService(manager.Object).InsertComponent(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void InsertComponent_NotAssembly_Throws()
    {
        var manager = ConnectedNonAssy();
        var filePath = CreateTempModelFile();
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                new AssemblyService(manager.Object).InsertComponent(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
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
    public void ResolveComponentTarget_WithHierarchyPath_ReturnsResolvedTargetAndReuseCount()
    {
        var (manager, assy) = ConnectedWithAssy();
        var doc = assy.As<IModelDoc2>();

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

        doc.Setup(d => d.GetPathName()).Returns(@"C:\TopLevel.sldasm");
        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { subAssembly.Object });

        var result = new AssemblyService(manager.Object).ResolveComponentTarget(hierarchyPath: "SubAsm-1/NestedPart-1");

        Assert.True(result.IsResolved);
        Assert.False(result.IsAmbiguous);
        Assert.NotNull(result.ResolvedInstance);
        Assert.Equal("SubAsm-1/NestedPart-1", result.ResolvedInstance!.HierarchyPath);
        Assert.Equal("SubAsm-1", result.OwningAssemblyHierarchyPath);
        Assert.Equal(@"C:\SubAsm.sldasm", result.OwningAssemblyFilePath);
        Assert.Equal(2, result.SourceFileReuseCount);
        Assert.Single(result.MatchingInstances);
        doc.Verify(d => d.ClearSelection2(true), Times.Never);
    }

    [Fact]
    public void ResolveComponentTarget_WithAmbiguousName_ReturnsAllCandidates()
    {
        var (manager, assy) = ConnectedWithAssy();

        var child1 = new Mock<Component2>();
        child1.Setup(c => c.Name2).Returns("Pulley-1");
        child1.Setup(c => c.GetPathName()).Returns(@"C:\PulleyA.sldprt");
        child1.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var child2 = new Mock<Component2>();
        child2.Setup(c => c.Name2).Returns("Pulley-1");
        child2.Setup(c => c.GetPathName()).Returns(@"C:\PulleyB.sldprt");
        child2.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var leftSubAssembly = new Mock<Component2>();
        leftSubAssembly.Setup(c => c.Name2).Returns("LeftSubAsm-1");
        leftSubAssembly.Setup(c => c.GetPathName()).Returns(@"C:\LeftSubAsm.sldasm");
        leftSubAssembly.Setup(c => c.GetChildren()).Returns(new object[] { child1.Object });

        var rightSubAssembly = new Mock<Component2>();
        rightSubAssembly.Setup(c => c.Name2).Returns("RightSubAsm-1");
        rightSubAssembly.Setup(c => c.GetPathName()).Returns(@"C:\RightSubAsm.sldasm");
        rightSubAssembly.Setup(c => c.GetChildren()).Returns(new object[] { child2.Object });

        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { leftSubAssembly.Object, rightSubAssembly.Object });

        var result = new AssemblyService(manager.Object).ResolveComponentTarget(componentName: "Pulley-1");

        Assert.False(result.IsResolved);
        Assert.True(result.IsAmbiguous);
        Assert.Null(result.ResolvedInstance);
        Assert.Equal(2, result.MatchingInstances.Count);
        Assert.Contains(result.MatchingInstances, c => c.HierarchyPath == "LeftSubAsm-1/Pulley-1");
        Assert.Contains(result.MatchingInstances, c => c.HierarchyPath == "RightSubAsm-1/Pulley-1");
    }

    [Fact]
    public void ResolveComponentTarget_WhenDirectParentHasNoPath_UsesNearestAncestorAssemblyPath()
    {
        var (manager, assy) = ConnectedWithAssy();
        var doc = assy.As<IModelDoc2>();

        var nestedPart = new Mock<Component2>();
        nestedPart.Setup(c => c.Name2).Returns("Pulley-1");
        nestedPart.Setup(c => c.GetPathName()).Returns(@"C:\Pulley.sldprt");
        nestedPart.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var nestedRoot = new Mock<Component2>();
        nestedRoot.Setup(c => c.Name2).Returns("SubAsm-1");
        nestedRoot.Setup(c => c.GetPathName()).Returns(string.Empty);
        nestedRoot.Setup(c => c.GetChildren()).Returns(new object[] { nestedPart.Object });

        var topLevelSubAssembly = new Mock<Component2>();
        topLevelSubAssembly.Setup(c => c.Name2).Returns("SubAsm-1");
        topLevelSubAssembly.Setup(c => c.GetPathName()).Returns(@"C:\SubAsm.sldasm");
        topLevelSubAssembly.Setup(c => c.GetChildren()).Returns(new object[] { nestedRoot.Object });

        doc.Setup(d => d.GetPathName()).Returns(@"C:\TopLevel.sldasm");
        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { topLevelSubAssembly.Object });

        var result = new AssemblyService(manager.Object).ResolveComponentTarget(hierarchyPath: "SubAsm-1/SubAsm-1/Pulley-1");

        Assert.True(result.IsResolved);
        Assert.Equal("SubAsm-1/SubAsm-1", result.OwningAssemblyHierarchyPath);
        Assert.Equal(@"C:\SubAsm.sldasm", result.OwningAssemblyFilePath);
    }

    [Fact]
    public void ResolveComponentTarget_WithoutCriteria_Throws()
    {
        var (manager, _) = ConnectedWithAssy();

        Assert.Throws<ArgumentException>(() => new AssemblyService(manager.Object).ResolveComponentTarget());
    }

    [Fact]
    public void ResolveComponentTarget_WhenActiveDocumentIsNotAssembly_Throws()
    {
        var manager = ConnectedNonAssy();

        Assert.Throws<InvalidOperationException>(() => new AssemblyService(manager.Object).ResolveComponentTarget(hierarchyPath: "Part-1"));
    }

    [Fact]
    public void AnalyzeSharedPartEditImpact_ForSingleUsePart_ReturnsSafeDirectEdit()
    {
        var (manager, assy) = ConnectedWithAssy();
        var doc = assy.As<IModelDoc2>();

        var part = new Mock<Component2>();
        part.Setup(c => c.Name2).Returns("Bracket-1");
        part.Setup(c => c.GetPathName()).Returns(@"C:\Bracket.sldprt");
        part.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        doc.Setup(d => d.GetPathName()).Returns(@"C:\TopLevel.sldasm");
        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { part.Object });

        var result = new AssemblyService(manager.Object).AnalyzeSharedPartEditImpact(hierarchyPath: "Bracket-1");

        Assert.True(result.TargetResolution.IsResolved);
        Assert.True(result.SafeDirectEdit);
        Assert.Equal("safe_direct_edit", result.RecommendedAction);
        Assert.Equal(1, result.AffectedInstanceCount);
        Assert.Single(result.AffectedInstances);
        doc.Verify(d => d.ClearSelection2(true), Times.Never);
        assy.Verify(a => a.ReplaceComponents2(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void AnalyzeSharedPartEditImpact_ForReusedPart_ReturnsAllAffectedPlacements()
    {
        var (manager, assy) = ConnectedWithAssy();

        var child1 = new Mock<Component2>();
        child1.Setup(c => c.Name2).Returns("Pulley-1");
        child1.Setup(c => c.GetPathName()).Returns(@"C:\Pulley.sldprt");
        child1.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var child2 = new Mock<Component2>();
        child2.Setup(c => c.Name2).Returns("Pulley-2");
        child2.Setup(c => c.GetPathName()).Returns(@"C:\Pulley.sldprt");
        child2.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var unrelated = new Mock<Component2>();
        unrelated.Setup(c => c.Name2).Returns("Bracket-1");
        unrelated.Setup(c => c.GetPathName()).Returns(@"C:\Bracket.sldprt");
        unrelated.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var subAssembly = new Mock<Component2>();
        subAssembly.Setup(c => c.Name2).Returns("SubAsm-1");
        subAssembly.Setup(c => c.GetPathName()).Returns(@"C:\SubAsm.sldasm");
        subAssembly.Setup(c => c.GetChildren()).Returns(new object[] { child1.Object, child2.Object, unrelated.Object });

        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { subAssembly.Object });

        var result = new AssemblyService(manager.Object).AnalyzeSharedPartEditImpact(hierarchyPath: "SubAsm-1/Pulley-1");

        Assert.True(result.TargetResolution.IsResolved);
        Assert.False(result.SafeDirectEdit);
        Assert.Equal("replace_single_instance_before_edit", result.RecommendedAction);
        Assert.Equal(2, result.AffectedInstanceCount);
        Assert.Contains(result.AffectedInstances, c => c.HierarchyPath == "SubAsm-1/Pulley-1");
        Assert.Contains(result.AffectedInstances, c => c.HierarchyPath == "SubAsm-1/Pulley-2");
    }

    [Fact]
    public void CheckInterference_ReturnsInterferingInstances()
    {
        var (manager, assy) = ConnectedWithAssy();
        var doc = assy.As<IModelDoc2>();

        var part1 = new Mock<Component2>();
        part1.Setup(c => c.Name2).Returns("Part1-1");
        part1.Setup(c => c.GetPathName()).Returns(@"C:\Part1.sldprt");
        part1.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var part2 = new Mock<Component2>();
        part2.Setup(c => c.Name2).Returns("Part2-1");
        part2.Setup(c => c.GetPathName()).Returns(@"C:\Part2.sldprt");
        part2.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
        part1.Setup(c => c.Select2(false, 0)).Returns(true);
        part2.Setup(c => c.Select2(true, 0)).Returns(true);
        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { part1.Object, part2.Object });
        SetupToolsCheckInterference2(
            assy,
            checkedComponentCount: 2,
            treatCoincidenceAsInterference: false,
            interferingComponents: new object[] { part2.Object, part1.Object },
            interferingFaces: new object[] { new Mock<Face2>().Object, new Mock<Face2>().Object });

        var result = new AssemblyService(manager.Object).CheckInterference();

        Assert.True(result.HasInterference);
        Assert.False(result.TreatCoincidenceAsInterference);
        Assert.Equal(2, result.CheckedComponentCount);
        Assert.Equal(2, result.InterferingFaceCount);
        Assert.Equal(2, result.InterferingComponents.Count);
        doc.Verify(d => d.ClearSelection2(true), Times.Exactly(2));
        assy.Verify(a => a.ToolsCheckInterference2(
            2,
            It.IsAny<object>(),
            false,
            out It.Ref<object>.IsAny,
            out It.Ref<object>.IsAny), Times.Once);
    }

    [Fact]
    public void CheckInterference_WithHierarchyFilter_ChecksSubset()
    {
        var (manager, assy) = ConnectedWithAssy();
        var doc = assy.As<IModelDoc2>();

        var child = new Mock<Component2>();
        child.Setup(c => c.Name2).Returns("NestedPart-1");
        child.Setup(c => c.GetPathName()).Returns(@"C:\NestedPart.sldprt");
        child.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var sibling = new Mock<Component2>();
        sibling.Setup(c => c.Name2).Returns("NestedPart-2");
        sibling.Setup(c => c.GetPathName()).Returns(@"C:\NestedPart2.sldprt");
        sibling.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
        child.Setup(c => c.Select2(false, 0)).Returns(true);
        sibling.Setup(c => c.Select2(true, 0)).Returns(true);

        var subAssembly = new Mock<Component2>();
        subAssembly.Setup(c => c.Name2).Returns("SubAsm-1");
        subAssembly.Setup(c => c.GetPathName()).Returns(@"C:\SubAsm.sldasm");
        subAssembly.Setup(c => c.GetChildren()).Returns(new object[] { child.Object, sibling.Object });

        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { subAssembly.Object });
        SetupToolsCheckInterference2(
            assy,
            checkedComponentCount: 2,
            treatCoincidenceAsInterference: true,
            interferingComponents: new object[] { child.Object, sibling.Object },
            interferingFaces: new object[] { new Mock<Face2>().Object, new Mock<Face2>().Object });

        var result = new AssemblyService(manager.Object).CheckInterference(
            ["SubAsm-1/NestedPart-1", "SubAsm-1/NestedPart-2"],
            treatCoincidenceAsInterference: true);

        Assert.True(result.HasInterference);
        Assert.True(result.TreatCoincidenceAsInterference);
        Assert.Equal(2, result.CheckedComponentCount);
        Assert.Equal(2, result.InterferingFaceCount);
        Assert.Equal(2, result.InterferingComponents.Count);
        doc.Verify(d => d.ClearSelection2(true), Times.Exactly(2));
        assy.Verify(a => a.ToolsCheckInterference2(
            2,
            It.IsAny<object>(),
            true,
            out It.Ref<object>.IsAny,
            out It.Ref<object>.IsAny), Times.Once);
    }

    [Fact]
    public void CheckInterference_WhenFilterDoesNotMatch_ExcludesInterference()
    {
        var (manager, assy) = ConnectedWithAssy();
        var part1 = new Mock<Component2>();
        part1.Setup(c => c.Name2).Returns("Part1-1");
        part1.Setup(c => c.GetPathName()).Returns(@"C:\Part1.sldprt");
        part1.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
        var part2 = new Mock<Component2>();
        part2.Setup(c => c.Name2).Returns("Part2-1");
        part2.Setup(c => c.GetPathName()).Returns(@"C:\Part2.sldprt");
        part2.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { part1.Object, part2.Object });

        var result = new AssemblyService(manager.Object).CheckInterference(["Missing/Part-1"]);

        Assert.False(result.HasInterference);
        Assert.Equal(0, result.CheckedComponentCount);
        Assert.Empty(result.InterferingComponents);
        assy.Verify(a => a.ToolsCheckInterference2(
            It.IsAny<int>(),
            It.IsAny<object>(),
            It.IsAny<bool>(),
            out It.Ref<object>.IsAny,
            out It.Ref<object>.IsAny), Times.Never);
    }

    [Fact]
    public void CheckInterference_WhenToolsCheckInterference2ServerFault_FallsBackToDetectionManager()
    {
        var (manager, assy) = ConnectedWithAssy();
        var part1 = new Mock<Component2>();
        var part2 = new Mock<Component2>();
        var detectionManager = new Mock<InterferenceDetectionMgr>();
        var interference = new Mock<Interference>();
        var serverFault = new COMException("server fault", unchecked((int)0x80010105));
        object ignoredComponents = Array.Empty<object>();
        object ignoredFaces = Array.Empty<object>();

        part1.Setup(c => c.Name2).Returns("Part1-1");
        part1.Setup(c => c.GetPathName()).Returns(@"C:\Part1.sldprt");
        part1.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
        part1.Setup(c => c.Select2(false, 0)).Returns(true);

        part2.Setup(c => c.Name2).Returns("Part2-1");
        part2.Setup(c => c.GetPathName()).Returns(@"C:\Part2.sldprt");
        part2.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());
        part2.Setup(c => c.Select2(true, 0)).Returns(true);

        assy.Setup(a => a.GetComponents(true)).Returns(new object[] { part1.Object, part2.Object });
        assy.Setup(a => a.ToolsCheckInterference2(
                2,
                It.IsAny<object>(),
                false,
                out ignoredComponents,
                out ignoredFaces))
            .Throws(serverFault);
        assy.Setup(a => a.ToolsCheckInterference());
        assy.SetupGet(a => a.InterferenceDetectionManager).Returns(detectionManager.Object);

        interference.Setup(i => i.Components).Returns(new object[] { part1.Object, part2.Object });
        interference.Setup(i => i.IsPossibleInterference).Returns(false);
        interference.Setup(i => i.GetComponentCount()).Returns(2);
        detectionManager.Setup(m => m.GetInterferences()).Returns(new object[] { interference.Object });

        var result = new AssemblyService(manager.Object).CheckInterference();

        Assert.True(result.HasInterference);
        Assert.Equal(2, result.CheckedComponentCount);
        Assert.Equal(2, result.InterferingFaceCount);
        detectionManager.Verify(m => m.GetInterferences(), Times.Once);
        detectionManager.Verify(m => m.Done(), Times.Once);
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
