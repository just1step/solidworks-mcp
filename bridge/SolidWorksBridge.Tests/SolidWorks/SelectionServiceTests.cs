using Moq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
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

    private static (Mock<ISwConnectionManager> manager,
                    Mock<ISldWorksApp> swApp,
                    Mock<IPartDoc> part,
                    Mock<IModelDoc2> model)
        ConnectedWithPartDoc(params Mock<IBody2>[] bodies)
    {
        var part = new Mock<IPartDoc>();
        var model = part.As<IModelDoc2>();
        part.Setup(p => p.GetBodies2(It.IsAny<int>(), true))
            .Returns(bodies.Select(body => (object)body.Object).ToArray());

        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.IActiveDoc2).Returns(model.Object);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        return (manager, swApp, part, model);
    }

    private static (Mock<ISwConnectionManager> manager,
                    Mock<ISldWorksApp> swApp,
                    Mock<IAssemblyDoc> assembly,
                    Mock<IModelDoc2> model)
        ConnectedWithAssemblyDoc(params Mock<Component2>[] components)
    {
        var assembly = new Mock<IAssemblyDoc>();
        var model = assembly.As<IModelDoc2>();
        assembly.Setup(a => a.GetComponents(true))
            .Returns(components.Select(component => (object)component.Object).ToArray());

        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.IActiveDoc2).Returns(model.Object);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        return (manager, swApp, assembly, model);
    }

    private static Mock<Component2> Component(string name, string path, params Mock<Component2>[] children)
    {
        var component = new Mock<Component2>();
        component.Setup(c => c.Name2).Returns(name);
        component.Setup(c => c.GetPathName()).Returns(path);
        component.Setup(c => c.GetChildren()).Returns(children.Select(child => (object)child.Object).ToArray());
        return component;
    }

    private static Mock<IBody2> BodyWith(params object[] entities)
    {
        var body = new Mock<IBody2>();
        body.Setup(b => b.GetFaces()).Returns(entities.OfType<IFace2>().Cast<object>().ToArray());
        body.Setup(b => b.GetEdges()).Returns(entities.OfType<IEdge>().Cast<object>().ToArray());
        body.Setup(b => b.GetVertices()).Returns(entities.OfType<IVertex>().Cast<object>().ToArray());
        return body;
    }

    private static Mock<IFace2> Face(double[]? box = null)
    {
        var face = new Mock<IFace2>();
        face.As<IEntity>();
        face.Setup(f => f.GetBox()).Returns(box);
        return face;
    }

    private static Mock<IVertex> Vertex(params double[] point)
    {
        var vertex = new Mock<IVertex>();
        vertex.As<IEntity>();
        vertex.Setup(v => v.GetPoint()).Returns(point);
        return vertex;
    }

    private static Mock<IEdge> Edge(Mock<IVertex>? start = null, Mock<IVertex>? end = null)
    {
        var edge = new Mock<IEdge>();
        edge.As<IEntity>();
        edge.Setup(e => e.GetStartVertex()).Returns(start?.Object);
        edge.Setup(e => e.GetEndVertex()).Returns(end?.Object);
        return edge;
    }

    private static Feature RefPlaneFeature(string name, string selectionName, string selectionType, Feature? next = null)
    {
        var feature = new Mock<Feature>();
        feature.Setup(f => f.Name).Returns(name);
        feature.Setup(f => f.GetTypeName2()).Returns("RefPlane");
        feature.Setup(f => f.GetNameForSelection(out selectionType)).Returns(selectionName);
        feature.Setup(f => f.GetNextFeature()).Returns(next);
        return feature.Object;
    }

    private static Feature NonPlaneFeature(string name, string typeName, Feature? next = null)
    {
        var feature = new Mock<Feature>();
        feature.Setup(f => f.Name).Returns(name);
        feature.Setup(f => f.GetTypeName2()).Returns(typeName);
        feature.Setup(f => f.GetNextFeature()).Returns(next);
        return feature.Object;
    }

    private static Feature FeatureNode(
        string name,
        string typeName,
        object[]? children = null,
        Feature? next = null,
        bool canSelect = true,
        int errorCode = 0,
        bool isWarning = false,
        object? specificFeature = null)
    {
        var feature = new Mock<Feature>();
        feature.Setup(f => f.Name).Returns(name);
        feature.Setup(f => f.GetTypeName2()).Returns(typeName);
        feature.Setup(f => f.GetNextFeature()).Returns(next);
        feature.Setup(f => f.GetChildren()).Returns(children ?? Array.Empty<object>());
        feature.Setup(f => f.Select2(false, -1)).Returns(canSelect);
        feature.Setup(f => f.GetSpecificFeature2()).Returns(specificFeature);
        var warningOut = isWarning;
        feature.Setup(f => f.GetErrorCode2(out warningOut)).Returns(errorCode);
        return feature.Object;
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
        doc.Verify(d => d.ClearSelection2(true), Times.Once);
        doc.Verify(d => d.SelectByID("Front Plane", "PLANE", 0, 0, 0), Times.Once);
    }

    [Fact]
    public void SelectByName_RetriesKnownAlias_WhenOriginalTypeFails()
    {
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.SelectByID("Front Plane", "swSelDATUMPLANES", 0, 0, 0)).Returns(false);
        doc.Setup(d => d.SelectByID("Front Plane", "PLANE", 0, 0, 0)).Returns(true);

        var result = new SelectionService(manager.Object).SelectByName("Front Plane", "swSelDATUMPLANES");

        Assert.True(result.Success);
        Assert.Contains("PLANE", result.Message);
        doc.Verify(d => d.ClearSelection2(true), Times.Once);
        doc.Verify(d => d.SelectByID("Front Plane", "swSelDATUMPLANES", 0, 0, 0), Times.Once);
        doc.Verify(d => d.SelectByID("Front Plane", "PLANE", 0, 0, 0), Times.Once);
    }

    [Fact]
    public void SelectByName_FallsBackToLocalizedStandardPlane_WhenEnglishNameFails()
    {
        var (manager, _, doc) = ConnectedWithDoc();

        var top = RefPlaneFeature("上视基准面", "上视基准面", "PLANE");
        var front = RefPlaneFeature("前视基准面", "前视基准面", "PLANE", top);

        doc.Setup(d => d.FirstFeature()).Returns(front);
        doc.Setup(d => d.SelectByID("Front Plane", "PLANE", 0, 0, 0)).Returns(false);
        doc.Setup(d => d.SelectByID("Front Plane", "swSelDATUMPLANES", 0, 0, 0)).Returns(false);
        doc.Setup(d => d.SelectByID("前视基准面", "PLANE", 0, 0, 0)).Returns(true);

        var result = new SelectionService(manager.Object).SelectByName("Front Plane", "PLANE");

        Assert.True(result.Success);
        Assert.Contains("localized fallback", result.Message);
        Assert.Contains("前视基准面", result.Message);
        doc.Verify(d => d.SelectByID("Front Plane", "PLANE", 0, 0, 0), Times.Once);
        doc.Verify(d => d.SelectByID("前视基准面", "PLANE", 0, 0, 0), Times.Once);
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
    // ListEntities
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void ListEntities_PartTopology_ReturnsIndexedFaceEdgeAndVertex()
    {
        var face = Face([0d, 0d, 0d, 0.01d, 0.02d, 0.03d]);
        var start = Vertex(0, 0, 0);
        var end = Vertex(0.02, 0.01, 0);
        var edge = Edge(start, end);
        var looseVertex = Vertex(0.04, 0.05, 0.06);
        var body = BodyWith(face.Object, edge.Object, looseVertex.Object);
        var (manager, _, _, _) = ConnectedWithPartDoc(body);

        var result = new SelectionService(manager.Object).ListEntities();

        Assert.Collection(result,
            item =>
            {
                Assert.Equal(0, item.Index);
                Assert.Equal(SelectableEntityType.Face, item.EntityType);
                Assert.Null(item.ComponentName);
                Assert.Equal([0d, 0d, 0d, 0.01d, 0.02d, 0.03d], item.Box);
            },
            item =>
            {
                Assert.Equal(1, item.Index);
                Assert.Equal(SelectableEntityType.Edge, item.EntityType);
                Assert.Equal([0d, 0d, 0d, 0.02d, 0.01d, 0d], item.Box);
            },
            item =>
            {
                Assert.Equal(2, item.Index);
                Assert.Equal(SelectableEntityType.Vertex, item.EntityType);
                Assert.Equal([0.04d, 0.05d, 0.06d, 0.04d, 0.05d, 0.06d], item.Box);
            });
    }

    [Fact]
    public void ListEntities_FilterByType_ReturnsMatchingEntitiesOnly()
    {
        var body = BodyWith(
            Face([0d, 0d, 0d, 1d, 1d, 1d]).Object,
            Edge(Vertex(0, 0, 0), Vertex(1, 0, 0)).Object,
            Vertex(2, 2, 2).Object);
        var (manager, _, _, _) = ConnectedWithPartDoc(body);

        var result = new SelectionService(manager.Object).ListEntities(SelectableEntityType.Edge);

        var entity = Assert.Single(result);
        Assert.Equal(0, entity.Index);
        Assert.Equal(SelectableEntityType.Edge, entity.EntityType);
    }

    [Fact]
    public void ListEntities_AssemblyRecursesIntoNestedSubassemblyParts()
    {
        var face = Face([0d, 0.01d, 0d, 0.05d, 0.01d, 0.03d]);
        object partBodiesInfo = null!;
        var partComponent = new Mock<Component2>();
        partComponent.Setup(c => c.Name2).Returns("2020铝板-1");
        partComponent.Setup(c => c.GetBodies3((int)swBodyType_e.swSolidBody, out partBodiesInfo))
            .Returns(new object[] { BodyWith(face.Object).Object });
        partComponent.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        object subAssemblyBodiesInfo = null!;
        var subAssembly = new Mock<Component2>();
        subAssembly.Setup(c => c.Name2).Returns("底部移动平台-1");
        subAssembly.Setup(c => c.GetBodies3((int)swBodyType_e.swSolidBody, out subAssemblyBodiesInfo))
            .Returns(Array.Empty<object>());
        subAssembly.Setup(c => c.GetChildren()).Returns(new object[] { partComponent.Object });

        var assembly = new Mock<IAssemblyDoc>();
        var model = assembly.As<IModelDoc2>();
        assembly.Setup(a => a.GetComponents(true)).Returns(new object[] { subAssembly.Object });

        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.IActiveDoc2).Returns(model.Object);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        var result = new SelectionService(manager.Object).ListEntities(SelectableEntityType.Face, "2020铝板-1");

        var entity = Assert.Single(result);
        Assert.Equal(0, entity.Index);
        Assert.Equal("2020铝板-1", entity.ComponentName);
        Assert.Equal([0d, 0.01d, 0d, 0.05d, 0.01d, 0.03d], entity.Box);
    }

    [Fact]
    public void ListEntities_NoActiveDocument_ThrowsInvalidOperation()
    {
        var manager = ConnectedNoDoc();

        Assert.Throws<InvalidOperationException>(() => new SelectionService(manager.Object).ListEntities());
    }

    [Fact]
    public void GetSolidWorksContext_ReturnsLanguageAndReferencePlanes()
    {
        var third = RefPlaneFeature("Right Plane", "Right Plane", "PLANE");
        var second = RefPlaneFeature("Top Plane", "Top Plane", "PLANE", third);
        var ignored = NonPlaneFeature("Boss-Extrude1", "Boss", second);
        var first = RefPlaneFeature("Front Plane", "Front Plane", "PLANE", ignored);

        var (manager, swApp, doc) = ConnectedWithDoc();
        swApp.Setup(app => app.GetCurrentLanguage()).Returns("english");
        doc.Setup(d => d.FirstFeature()).Returns(first);

        var result = new SelectionService(manager.Object).GetSolidWorksContext();

        Assert.Equal("english", result.CurrentLanguage);
        Assert.Collection(result.ReferencePlanes,
            plane =>
            {
                Assert.Equal(0, plane.Index);
                Assert.Equal("Front Plane", plane.Name);
                Assert.Equal("Front Plane", plane.SelectionName);
                Assert.Equal("PLANE", plane.SelectionType);
            },
            plane =>
            {
                Assert.Equal(1, plane.Index);
                Assert.Equal("Top Plane", plane.Name);
            },
            plane =>
            {
                Assert.Equal(2, plane.Index);
                Assert.Equal("Right Plane", plane.Name);
            });
    }

    [Fact]
    public void ListFeatureTree_ReturnsFeatureMetadata()
    {
        var cut = FeatureNode("Cut-Extrude1", "CutExtrude");
        var looseSketch = FeatureNode("Sketch3", "ProfileFeature", next: cut);
        var usedSketch = FeatureNode("Sketch2", "ProfileFeature", children: [new object()], next: looseSketch);

        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.FirstFeature()).Returns(usedSketch);

        var result = new SelectionService(manager.Object).ListFeatureTree();

        Assert.Collection(result,
            item =>
            {
                Assert.Equal(0, item.Index);
                Assert.Equal("Sketch2", item.Name);
                Assert.Equal("ProfileFeature", item.TypeName);
                Assert.True(item.IsSketch);
                Assert.True(item.HasChildren);
            },
            item =>
            {
                Assert.Equal(1, item.Index);
                Assert.Equal("Sketch3", item.Name);
                Assert.True(item.IsSketch);
                Assert.False(item.HasChildren);
            },
            item =>
            {
                Assert.Equal(2, item.Index);
                Assert.Equal("Cut-Extrude1", item.Name);
                Assert.False(item.IsSketch);
            });
    }

    [Fact]
    public void ListModelHealthSensors_ReturnsStructuredSensorMetadata()
    {
        var sensor = new Mock<ISensor>();
        sensor.SetupGet(s => s.SensorType).Returns((int)swSensorType_e.swSensorDimension);
        sensor.SetupGet(s => s.SensorAlertEnabled).Returns(true);
        sensor.SetupGet(s => s.SensorAlertType).Returns((int)swSensorAlertType_e.swSensorAlert_GreaterThan);
        sensor.SetupGet(s => s.SensorAlertValue1).Returns(10d);
        sensor.SetupGet(s => s.SensorAlertValue2).Returns(0d);
        sensor.SetupGet(s => s.SensorAlertState).Returns(true);
        sensor.Setup(s => s.GetSensorFeatureData()).Returns(new object());
        double sensorValue = 12.5d;
        string sensorUnits = "mm";
        sensor.Setup(s => s.GetSensorValue(out sensorValue, out sensorUnits)).Returns(true);

        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.FirstFeature()).Returns(FeatureNode("ThicknessSensor", "Sensor", specificFeature: sensor.Object));
        doc.Setup(d => d.GetPathName()).Returns(@"C:\Part.sldprt");
        doc.Setup(d => d.GetTitle()).Returns("Part");

        var result = new SelectionService(manager.Object).ListModelHealthSensors();

        var item = Assert.Single(result);
        Assert.Equal(0, item.Index);
        Assert.Equal("ThicknessSensor", item.Name);
        Assert.Equal("Sensor", item.TypeName);
        Assert.Equal(@"C:\Part.sldprt", item.DocumentPath);
        Assert.Equal(@"C:\Part.sldprt", item.DocumentReference);
        Assert.Equal("swSensorDimension", item.SensorType);
        Assert.Equal("swSensorAlert_GreaterThan", item.AlertType);
        Assert.True(item.AlertEnabled);
        Assert.True(item.AlertTriggered);
        Assert.Equal(10d, item.AlertValue1);
        Assert.Equal(0d, item.AlertValue2);
        Assert.Equal("> 10 mm", item.ThresholdDescription);
        Assert.Equal(12.5d, item.CurrentValue);
        Assert.Equal("mm", item.Units);
        Assert.Equal("Object", item.FeatureDataType);
        Assert.Equal("completed", item.Status);
        Assert.Null(item.FailureReason);
    }

    [Fact]
    public void GetFeatureDiagnostics_ReturnsFeatureErrorCodesAndWhatsWrongItems()
    {
        var warningFeature = FeatureNode("MateGroup", "MateGroup", errorCode: 48, isWarning: true);
        var errorFeature = FeatureNode("MateCoincident1", "MateCoincident", next: warningFeature, errorCode: 46, isWarning: false);
        var extension = new Mock<ModelDocExtension>();
        object whatsWrongFeatures = new object[] { errorFeature, warningFeature };
        object whatsWrongCodes = new object[] { 46, 48 };
        object whatsWrongWarnings = new object[] { false, true };

        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.FirstFeature()).Returns(errorFeature);
        doc.Setup(d => d.GetActiveSketch2()).Returns((object?)null);
        doc.SetupGet(d => d.Extension).Returns(extension.Object);
        extension.Setup(e => e.GetWhatsWrong(out whatsWrongFeatures, out whatsWrongCodes, out whatsWrongWarnings)).Returns(true);

        var result = new SelectionService(manager.Object).GetFeatureDiagnostics();

        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);
        Assert.Collection(result.FeatureDiagnostics,
            item =>
            {
                Assert.Equal("MateCoincident1", item.Name);
                Assert.True(item.HasIssue);
                Assert.False(item.IsWarning);
                Assert.Equal(46, item.ErrorCode);
                Assert.Equal("swFeatureErrorMateOverdefined", item.ErrorName);
                Assert.True(item.AppearsInWhatsWrong);
            },
            item =>
            {
                Assert.Equal("MateGroup", item.Name);
                Assert.True(item.HasIssue);
                Assert.True(item.IsWarning);
                Assert.Equal(48, item.ErrorCode);
                Assert.Equal("swFeatureErrorMateBroken", item.ErrorName);
                Assert.True(item.AppearsInWhatsWrong);
            });
        Assert.Collection(result.WhatsWrongItems,
            item =>
            {
                Assert.Equal("MateCoincident1", item.Name);
                Assert.False(item.IsWarning);
                Assert.Equal(46, item.ErrorCode);
            },
            item =>
            {
                Assert.Equal("MateGroup", item.Name);
                Assert.True(item.IsWarning);
                Assert.Equal(48, item.ErrorCode);
            });
    }

    [Fact]
    public void GetFeatureDiagnostics_InAssembly_CorrelatesMateIssueToAssemblyLevelContext()
    {
        var mateFeature = FeatureNode("MateCoincident1", "MateCoincident", errorCode: 46, isWarning: false);
        var extension = new Mock<ModelDocExtension>();
        object whatsWrongFeatures = new object[] { mateFeature };
        object whatsWrongCodes = new object[] { 46 };
        object whatsWrongWarnings = new object[] { false };

        var (manager, _, _, model) = ConnectedWithAssemblyDoc();
        model.Setup(d => d.FirstFeature()).Returns(mateFeature);
        model.Setup(d => d.GetActiveSketch2()).Returns((object?)null);
        model.Setup(d => d.GetPathName()).Returns(@"C:\Assemblies\Top.sldasm");
        model.SetupGet(d => d.Extension).Returns(extension.Object);
        extension.Setup(e => e.GetWhatsWrong(out whatsWrongFeatures, out whatsWrongCodes, out whatsWrongWarnings)).Returns(true);

        var result = new SelectionService(manager.Object).GetFeatureDiagnostics();

        var issue = Assert.Single(result.CorrelatedIssues!);
        Assert.Equal("assembly_level", issue.TargetContext.ScopeType);
        Assert.True(issue.TargetContext.IsExact);
        Assert.False(issue.TargetContext.IsAmbiguous);
        Assert.Equal(Path.GetFullPath(@"C:\Assemblies\Top.sldasm"), issue.TargetContext.DocumentPath);
        Assert.Equal(Path.GetFullPath(@"C:\Assemblies\Top.sldasm"), issue.TargetContext.OwningAssemblyFilePath);
        Assert.Empty(issue.TargetContext.MatchingInstances);
    }

    [Fact]
    public void GetFeatureDiagnostics_InAssembly_CorrelatesComponentIssueToExactHierarchyPath()
    {
        var bracket = Component("Bracket-1", @"C:\Parts\Bracket.sldprt");
        var componentFeature = FeatureNode(
            "Bracket-1",
            "Reference",
            errorCode: 46,
            isWarning: false,
            specificFeature: bracket.Object);
        var extension = new Mock<ModelDocExtension>();
        object whatsWrongFeatures = new object[] { componentFeature };
        object whatsWrongCodes = new object[] { 46 };
        object whatsWrongWarnings = new object[] { false };

        var (manager, _, _, model) = ConnectedWithAssemblyDoc(bracket);
        model.Setup(d => d.FirstFeature()).Returns(componentFeature);
        model.Setup(d => d.GetActiveSketch2()).Returns((object?)null);
        model.Setup(d => d.GetPathName()).Returns(@"C:\Assemblies\Top.sldasm");
        model.SetupGet(d => d.Extension).Returns(extension.Object);
        extension.Setup(e => e.GetWhatsWrong(out whatsWrongFeatures, out whatsWrongCodes, out whatsWrongWarnings)).Returns(true);

        var result = new SelectionService(manager.Object).GetFeatureDiagnostics();

        var issue = Assert.Single(result.CorrelatedIssues!);
        Assert.Equal("component_instance", issue.TargetContext.ScopeType);
        Assert.True(issue.TargetContext.IsExact);
        Assert.False(issue.TargetContext.IsAmbiguous);
        Assert.Equal("Bracket-1", issue.TargetContext.ComponentName);
        Assert.Equal("Bracket-1", issue.TargetContext.HierarchyPath);
        Assert.Equal(Path.GetFullPath(@"C:\Parts\Bracket.sldprt"), issue.TargetContext.SourceFilePath);
        Assert.Equal(Path.GetFullPath(@"C:\Assemblies\Top.sldasm"), issue.TargetContext.OwningAssemblyFilePath);
        Assert.Equal(1, issue.TargetContext.SourceFileReuseCount);
        var match = Assert.Single(issue.TargetContext.MatchingInstances);
        Assert.Equal("Bracket-1", match.HierarchyPath);
    }

    [Fact]
    public void GetFeatureDiagnostics_InAssembly_FlagsAmbiguousSharedSourceScope()
    {
        var pulley1 = Component("Pulley-1", @"C:\Parts\Pulley.sldprt");
        var pulley2 = Component("Pulley-2", @"C:\Parts\Pulley.sldprt");
        var ambiguousFeature = FeatureNode("Pulley", "Reference", errorCode: 46, isWarning: false);
        var extension = new Mock<ModelDocExtension>();
        object whatsWrongFeatures = new object[] { ambiguousFeature };
        object whatsWrongCodes = new object[] { 46 };
        object whatsWrongWarnings = new object[] { false };

        var (manager, _, _, model) = ConnectedWithAssemblyDoc(pulley1, pulley2);
        model.Setup(d => d.FirstFeature()).Returns(ambiguousFeature);
        model.Setup(d => d.GetActiveSketch2()).Returns((object?)null);
        model.Setup(d => d.GetPathName()).Returns(@"C:\Assemblies\Top.sldasm");
        model.SetupGet(d => d.Extension).Returns(extension.Object);
        extension.Setup(e => e.GetWhatsWrong(out whatsWrongFeatures, out whatsWrongCodes, out whatsWrongWarnings)).Returns(true);

        var result = new SelectionService(manager.Object).GetFeatureDiagnostics();

        var issue = Assert.Single(result.CorrelatedIssues!);
        Assert.Equal("shared_source_scope", issue.TargetContext.ScopeType);
        Assert.False(issue.TargetContext.IsExact);
        Assert.True(issue.TargetContext.IsAmbiguous);
        Assert.Null(issue.TargetContext.HierarchyPath);
        Assert.Equal(Path.GetFullPath(@"C:\Parts\Pulley.sldprt"), issue.TargetContext.SourceFilePath);
        Assert.Equal(2, issue.TargetContext.SourceFileReuseCount);
        Assert.Equal(2, issue.TargetContext.MatchingInstances.Count);
        Assert.Contains(issue.TargetContext.MatchingInstances, item => item.HierarchyPath == "Pulley-1");
        Assert.Contains(issue.TargetContext.MatchingInstances, item => item.HierarchyPath == "Pulley-2");
    }

    [Fact]
    public void GetFeatureDiagnostics_WhenSketchIsActive_Throws()
    {
        var sketch = FeatureNode("Sketch9", "ProfileFeature");
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.FirstFeature()).Returns(sketch);
        doc.Setup(d => d.GetActiveSketch2()).Returns(new object());

        var error = Assert.Throws<InvalidOperationException>(() => new SelectionService(manager.Object).GetFeatureDiagnostics());

        Assert.Contains("Finish the active sketch", error.Message);
    }

    [Fact]
    public void ListReferencePlanes_NoActiveDocument_ThrowsInvalidOperation()
    {
        var manager = ConnectedNoDoc();

        Assert.Throws<InvalidOperationException>(() => new SelectionService(manager.Object).ListReferencePlanes());
    }

    // ─────────────────────────────────────────────────────────────
    // SelectEntity
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void SelectEntity_Success_UsesIndexAndSelectionMark()
    {
        var face = Face([0d, 0d, 0d, 1d, 1d, 1d]);
        var entity = face.As<IEntity>();
        var selectData = new Mock<SelectData>();
        selectData.SetupProperty(data => data.Mark);
        var selectionManager = new Mock<SelectionMgr>();
        selectionManager.Setup(m => m.CreateSelectData()).Returns(selectData.Object);

        var (manager, _, _, model) = ConnectedWithPartDoc(BodyWith(face.Object));
        model.Setup(d => d.ISelectionManager).Returns(selectionManager.Object);

        entity.Setup(e => e.Select4(true, It.IsAny<SelectData>()))
            .Callback<bool, SelectData>((_, data) => selectData = Mock.Get(Assert.IsAssignableFrom<SelectData>(data)))
            .Returns(true);

        var result = new SelectionService(manager.Object).SelectEntity(SelectableEntityType.Face, 0, append: true, mark: 7);

        Assert.True(result.Success);
        Assert.Equal(SelectableEntityType.Face, result.EntityType);
        Assert.Equal(0, result.EntityIndex);
        Assert.Null(result.ComponentName);
        Assert.Equal([0d, 0d, 0d, 1d, 1d, 1d], result.Box);
        Assert.Equal(7, selectData.Object.Mark);
        entity.Verify(e => e.Select4(true, It.IsAny<SelectData>()), Times.Once);
    }

    [Fact]
    public void SelectEntity_InAssemblyContext_EchoesOwningComponent()
    {
        var face = Face([0d, 0.01d, 0d, 0.05d, 0.01d, 0.03d]);
        var entity = face.As<IEntity>();
        var selectData = new Mock<SelectData>();
        var selectionManager = new Mock<SelectionMgr>();
        selectionManager.Setup(m => m.CreateSelectData()).Returns(selectData.Object);

        object partBodiesInfo = null!;
        var partComponent = new Mock<Component2>();
        partComponent.Setup(c => c.Name2).Returns("2020铝板-1");
        partComponent.Setup(c => c.GetBodies3((int)swBodyType_e.swSolidBody, out partBodiesInfo))
            .Returns(new object[] { BodyWith(face.Object).Object });
        partComponent.Setup(c => c.GetChildren()).Returns(Array.Empty<object>());

        var assembly = new Mock<IAssemblyDoc>();
        var model = assembly.As<IModelDoc2>();
        assembly.Setup(a => a.GetComponents(true)).Returns(new object[] { partComponent.Object });
        model.Setup(d => d.ISelectionManager).Returns(selectionManager.Object);

        var swApp = new Mock<ISldWorksApp>();
        swApp.Setup(s => s.IActiveDoc2).Returns(model.Object);

        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected());

        entity.Setup(e => e.Select4(false, It.IsAny<SelectData>())).Returns(true);

        var result = new SelectionService(manager.Object).SelectEntity(
            SelectableEntityType.Face,
            0,
            append: false,
            componentName: "2020铝板-1");

        Assert.True(result.Success);
        Assert.Equal(SelectableEntityType.Face, result.EntityType);
        Assert.Equal(0, result.EntityIndex);
        Assert.Equal("2020铝板-1", result.ComponentName);
        Assert.Equal([0d, 0.01d, 0d, 0.05d, 0.01d, 0.03d], result.Box);
    }

    [Fact]
    public void SelectEntity_WhenReplacingSelection_ClearsSelectionFirst()
    {
        var face = Face([0d, 0d, 0d, 1d, 1d, 1d]);
        var entity = face.As<IEntity>();
        var selectData = new Mock<SelectData>();
        var selectionManager = new Mock<SelectionMgr>();
        selectionManager.Setup(m => m.CreateSelectData()).Returns(selectData.Object);

        var (manager, _, _, model) = ConnectedWithPartDoc(BodyWith(face.Object));
        model.Setup(d => d.ISelectionManager).Returns(selectionManager.Object);
        entity.Setup(e => e.Select4(false, It.IsAny<SelectData>())).Returns(true);

        var result = new SelectionService(manager.Object).SelectEntity(SelectableEntityType.Face, 0, append: false);

        Assert.True(result.Success);
        model.Verify(d => d.ClearSelection2(true), Times.Once);
        entity.Verify(e => e.Select4(false, It.IsAny<SelectData>()), Times.Once);
    }

    [Fact]
    public void SelectEntity_MissingIndex_ReturnsFailureResult()
    {
        var (manager, _, _, _) = ConnectedWithPartDoc(BodyWith());

        var result = new SelectionService(manager.Object).SelectEntity(SelectableEntityType.Face, 0);

        Assert.False(result.Success);
        Assert.Contains("Could not find Face", result.Message);
    }

    [Fact]
    public void SelectEntity_NoActiveDocument_ThrowsInvalidOperation()
    {
        var manager = ConnectedNoDoc();

        Assert.Throws<InvalidOperationException>(() => new SelectionService(manager.Object).SelectEntity(SelectableEntityType.Face, 0));
    }

    [Fact]
    public void MeasureEntities_ReturnsOfficialMeasureValuesAndClearsSelection()
    {
        var faceA = Face([0d, 0d, 0d, 1d, 1d, 0d]);
        var faceB = Face([0d, 0d, 0.0005d, 1d, 1d, 0.0005d]);
        var entityA = faceA.As<IEntity>();
        var entityB = faceB.As<IEntity>();
        var selectionManager = new Mock<SelectionMgr>();
        var selectData = new Mock<SelectData>();
        var extension = new Mock<ModelDocExtension>();
        var measure = new Mock<Measure>();

        selectionManager.Setup(m => m.CreateSelectData()).Returns(selectData.Object);
        measure.SetupProperty(m => m.ArcOption);
        measure.Setup(m => m.Calculate(null)).Returns(true);
        measure.SetupGet(m => m.Distance).Returns(0.0005d);
        measure.SetupGet(m => m.NormalDistance).Returns(0.0005d);
        measure.SetupGet(m => m.CenterDistance).Returns(-1d);
        measure.SetupGet(m => m.Angle).Returns(-1d);
        measure.SetupGet(m => m.DeltaX).Returns(0d);
        measure.SetupGet(m => m.DeltaY).Returns(0d);
        measure.SetupGet(m => m.DeltaZ).Returns(0.0005d);
        measure.SetupGet(m => m.Projection).Returns(-1d);
        measure.SetupGet(m => m.X).Returns(-1d);
        measure.SetupGet(m => m.Y).Returns(-1d);
        measure.SetupGet(m => m.Z).Returns(-1d);
        measure.SetupGet(m => m.IsParallel).Returns(true);
        measure.SetupGet(m => m.IsPerpendicular).Returns(false);
        measure.SetupGet(m => m.IsIntersect).Returns(false);
        extension.Setup(e => e.CreateMeasure()).Returns(measure.Object);

        var (manager, _, _, model) = ConnectedWithPartDoc(BodyWith(faceA.Object, faceB.Object));
        model.Setup(d => d.ISelectionManager).Returns(selectionManager.Object);
        model.SetupGet(d => d.Extension).Returns(extension.Object);
        entityA.Setup(e => e.Select4(false, It.IsAny<SelectData>())).Returns(true);
        entityB.Setup(e => e.Select4(true, It.IsAny<SelectData>())).Returns(true);

        var result = new SelectionService(manager.Object).MeasureEntities(
            SelectableEntityType.Face,
            0,
            SelectableEntityType.Face,
            1,
            arcOption: 1);

        Assert.Equal(1, result.ArcOption);
        Assert.Equal(0.0005d, result.Distance);
        Assert.Equal(0.0005d, result.NormalDistance);
        Assert.Null(result.CenterDistance);
        Assert.Equal(0.0005d, result.DeltaZ);
        Assert.True(result.IsParallel);
        Assert.False(result.IsIntersect);
        Assert.Equal(SelectableEntityType.Face, result.FirstEntity.EntityType);
        Assert.Equal(0, result.FirstEntity.Index);
        Assert.Equal(1, result.SecondEntity.Index);
        Assert.Equal([0d, 0d, 0d, 1d, 1d, 0d], result.FirstEntity.Box);
        Assert.Equal([0d, 0d, 0.0005d, 1d, 1d, 0.0005d], result.SecondEntity.Box);
        Assert.Equal(1, measure.Object.ArcOption);
        model.Verify(d => d.ClearSelection2(true), Times.Exactly(2));
        entityA.Verify(e => e.Select4(false, It.IsAny<SelectData>()), Times.Once);
        entityB.Verify(e => e.Select4(true, It.IsAny<SelectData>()), Times.Once);
        measure.Verify(m => m.Calculate(null), Times.Once);
    }

    [Fact]
    public void MeasureEntities_WhenEntityMissing_Throws()
    {
        var (manager, _, _, _) = ConnectedWithPartDoc(BodyWith());

        var error = Assert.Throws<InvalidOperationException>(() => new SelectionService(manager.Object).MeasureEntities(
            SelectableEntityType.Face,
            0,
            SelectableEntityType.Face,
            1));

        Assert.Contains("Could not find Face", error.Message);
    }

    [Fact]
    public void MeasureEntities_WhenCalculateFails_Throws()
    {
        var faceA = Face([0d, 0d, 0d, 1d, 1d, 0d]);
        var faceB = Face([0d, 0d, 1d, 1d, 1d, 1d]);
        var entityA = faceA.As<IEntity>();
        var entityB = faceB.As<IEntity>();
        var selectionManager = new Mock<SelectionMgr>();
        var selectData = new Mock<SelectData>();
        var extension = new Mock<ModelDocExtension>();
        var measure = new Mock<Measure>();

        selectionManager.Setup(m => m.CreateSelectData()).Returns(selectData.Object);
        extension.Setup(e => e.CreateMeasure()).Returns(measure.Object);
        measure.SetupProperty(m => m.ArcOption);
        measure.Setup(m => m.Calculate(null)).Returns(false);

        var (manager, _, _, model) = ConnectedWithPartDoc(BodyWith(faceA.Object, faceB.Object));
        model.Setup(d => d.ISelectionManager).Returns(selectionManager.Object);
        model.SetupGet(d => d.Extension).Returns(extension.Object);
        entityA.Setup(e => e.Select4(false, It.IsAny<SelectData>())).Returns(true);
        entityB.Setup(e => e.Select4(true, It.IsAny<SelectData>())).Returns(true);

        var error = Assert.Throws<InvalidOperationException>(() => new SelectionService(manager.Object).MeasureEntities(
            SelectableEntityType.Face,
            0,
            SelectableEntityType.Face,
            1));

        Assert.Contains("could not measure", error.Message, StringComparison.OrdinalIgnoreCase);
        model.Verify(d => d.ClearSelection2(true), Times.Exactly(2));
    }

    [Fact]
    public void DeleteFeatureByName_DeletesMatchingFeature()
    {
        var sketch = FeatureNode("Sketch7", "ProfileFeature");
        var extension = new Mock<ModelDocExtension>();
        extension.Setup(e => e.DeleteSelection2(0)).Returns(true);

        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.FirstFeature()).Returns(sketch);
        doc.SetupGet(d => d.Extension).Returns(extension.Object);

        var result = new SelectionService(manager.Object).DeleteFeatureByName("Sketch7");

        Assert.True(result.Success);
        extension.Verify(e => e.DeleteSelection2(0), Times.Once);
        doc.Verify(d => d.ClearSelection2(true), Times.Exactly(2));
    }

    [Fact]
    public void DeleteFeatureByName_WhenSketchIsActive_Throws()
    {
        var sketch = FeatureNode("Sketch7", "ProfileFeature");
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.FirstFeature()).Returns(sketch);
        doc.Setup(d => d.GetActiveSketch2()).Returns(new object());

        var error = Assert.Throws<InvalidOperationException>(() => new SelectionService(manager.Object).DeleteFeatureByName("Sketch7"));

        Assert.Contains("Finish the active sketch", error.Message);
    }

    [Fact]
    public void DeleteUnusedSketches_DeletesOnlyLooseSketches()
    {
        var cut = FeatureNode("Boss-Extrude1", "BossExtrude");
        var looseSketch = FeatureNode("Sketch4", "ProfileFeature", next: cut);
        var usedSketch = FeatureNode("Sketch3", "ProfileFeature", children: [new object()], next: looseSketch);
        var extension = new Mock<ModelDocExtension>();
        extension.Setup(e => e.DeleteSelection2(0)).Returns(true);

        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.FirstFeature()).Returns(usedSketch);
        doc.SetupGet(d => d.Extension).Returns(extension.Object);
        doc.Setup(d => d.GetActiveSketch2()).Returns((object?)null);

        var result = new SelectionService(manager.Object).DeleteUnusedSketches();

        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(["Sketch4"], result.DeletedFeatureNames);
        Assert.Empty(result.FailedFeatureNames);
        extension.Verify(e => e.DeleteSelection2(0), Times.Once);
    }

    [Fact]
    public void DeleteUnusedSketches_WhenSketchIsActive_Throws()
    {
        var sketch = FeatureNode("Sketch9", "ProfileFeature");
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.FirstFeature()).Returns(sketch);
        doc.Setup(d => d.GetActiveSketch2()).Returns(new object());

        var error = Assert.Throws<InvalidOperationException>(() => new SelectionService(manager.Object).DeleteUnusedSketches());

        Assert.Contains("Finish the active sketch", error.Message);
    }

    [Fact]
    public void ListFeatureTree_WhenSketchIsActive_Throws()
    {
        var sketch = FeatureNode("Sketch9", "ProfileFeature");
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.FirstFeature()).Returns(sketch);
        doc.Setup(d => d.GetActiveSketch2()).Returns(new object());

        var error = Assert.Throws<InvalidOperationException>(() => new SelectionService(manager.Object).ListFeatureTree());

        Assert.Contains("Finish the active sketch", error.Message);
    }

    [Fact]
    public void GetEditState_WhenSketchIsActive_ReturnsUnsafeState()
    {
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.GetActiveSketch2()).Returns(new object());

        var state = new SelectionService(manager.Object).GetEditState();

        Assert.True(state.IsEditing);
        Assert.Equal("sketch", state.EditMode);
        Assert.False(state.CanReadFeatureTree);
        Assert.False(state.CanDeleteFeatures);
    }

    [Fact]
    public void GetEditState_WhenNotEditing_ReturnsSafeState()
    {
        var (manager, _, doc) = ConnectedWithDoc();
        doc.Setup(d => d.GetActiveSketch2()).Returns((object?)null);

        var state = new SelectionService(manager.Object).GetEditState();

        Assert.False(state.IsEditing);
        Assert.Equal("none", state.EditMode);
        Assert.True(state.CanReadFeatureTree);
        Assert.True(state.CanDeleteFeatures);
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
