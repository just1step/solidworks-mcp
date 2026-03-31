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
        Assert.Equal(7, selectData.Object.Mark);
        entity.Verify(e => e.Select4(true, It.IsAny<SelectData>()), Times.Once);
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
