using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

/// <summary>
/// Integration tests for FeatureService.
/// Requires a running SolidWorks instance.
/// Each test creates a fresh Part, draws a sketch on 前视基准面,
/// then applies a feature operation.
/// Run: dotnet test --filter "Category=Integration"
/// </summary>
[Collection("SolidWorks Integration")]
public class FeatureServiceIntegrationTests
{
    // ── Setup ─────────────────────────────────────────────────────

    private static (SelectionService sel, SketchService sketch, FeatureService feature)
        RealServices()
    {
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);
        manager.Connect();

        var docs = new DocumentService(manager);
        docs.NewDocument(SwDocType.Part);

        return (
            new SelectionService(manager),
            new SketchService(manager),
            new FeatureService(manager)
        );
    }

    /// <summary>
    /// Helper: select 前视基准面, open a sketch, draw a 100mm x 60mm rectangle.
    /// The sketch is LEFT OPEN (in edit mode) so that Extrude/Cut can immediately
    /// be called — SolidWorks closes the sketch internally during feature creation.
    /// </summary>
    private static void OpenRectSketch(SelectionService sel, SketchService sketch)
    {
        sel.SelectByName("前视基准面", "PLANE");
        sketch.InsertSketch();
        sketch.AddRectangle(-0.05, -0.03, 0.05, 0.03);
        // NOTE: do NOT call FinishSketch — Extrude will close the sketch internally
    }

    private static SketchPoint CreateSketchPointOnSelectedFace()
    {
        var manager = new SwConnectionManager(new SwComConnector());
        manager.Connect();

        var sketch = new SketchService(manager);
        sketch.InsertSketch();
        var point = manager.SwApp!.SketchManager!.CreatePoint(0, 0, 0)
            ?? throw new InvalidOperationException("Failed to create sketch point on the selected face.");
        sketch.FinishSketch();
        return point;
    }

    private static IFace2 SelectTopPlanarFace(SelectionService sel)
    {
        var topFace = sel.ListEntities(SelectableEntityType.Face)
            .Where(face => face.Box is { Length: >= 6 } box && Math.Abs(box[2] - box[5]) < 1e-9)
            .OrderByDescending(face => face.Box![5])
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No top planar face found on the active solid body.");

        var selected = sel.SelectEntity(SelectableEntityType.Face, topFace.Index);
        Assert.True(selected.Success, selected.Message);

        var manager = new SwConnectionManager(new SwComConnector());
        manager.Connect();
        var body = ((object[]?)((IPartDoc)manager.SwApp!.IActiveDoc2!).GetBodies2((int)swBodyType_e.swSolidBody, true)
            ?? Array.Empty<object>())
            .OfType<IBody2>()
            .First();

        return ((object[]?)body.GetFaces() ?? Array.Empty<object>())
            .OfType<IFace2>()
            .OrderByDescending(face => ((double[]?)face.GetBox())?[5] ?? double.MinValue)
            .First(face =>
            {
                var surface = face.GetSurface() as ISurface;
                return surface != null && surface.IsPlane();
            });
    }

    private static void SelectSketchPoint(SketchPoint point)
    {
        var manager = new SwConnectionManager(new SwComConnector());
        manager.Connect();

        var selectionManager = manager.SwApp!.IActiveDoc2!.ISelectionManager
            ?? throw new InvalidOperationException("No selection manager available.");
        var selectData = (SelectData)selectionManager.CreateSelectData();
        if (!point.Select4(true, selectData))
        {
            throw new InvalidOperationException("Failed to select sketch point for simple-hole integration test.");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Integration Tests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_Extrude_CreatesExtrudeFeature()
    {
        // Expected: extruding a 100x60mm rectangle 20mm deep creates a Boss-Extrude feature
        // NOTE: Extrude is called while sketch is still open — SW closes it internally
        var (sel, sketch, feature) = RealServices();
        OpenRectSketch(sel, sketch);

        var info = feature.Extrude(0.02);

        Assert.Equal("Extrude", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name),
            "Extrude should produce a named feature");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ExtrudeThenCut_CreatesCutFeature()
    {
        // Step 1: boss extrude a box (sketch open → Extrude closes it)
        // Step 2: open circle sketch on same plane → ExtrudeCut
        var (sel, sketch, feature) = RealServices();

        OpenRectSketch(sel, sketch);
        feature.Extrude(0.02);  // box 100x60x20mm

        // Draw a circle on 前视基准面 and extrude-cut through the box
        sel.SelectByName("前视基准面", "PLANE");
        sketch.InsertSketch();
        sketch.AddCircle(0, 0, 0.01);
        // Leave sketch open — ExtrudeCut closes it internally
        int faceCountBeforeCut = sel.ListEntities(SelectableEntityType.Face).Count;

        var info = feature.ExtrudeCut(0.05, EndCondition.ThroughAll);
        int faceCountAfterCut = sel.ListEntities(SelectableEntityType.Face).Count;

        Assert.Equal("ExtrudeCut", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
        Assert.True(faceCountAfterCut > faceCountBeforeCut,
            $"ExtrudeCut should change solid topology. Before={faceCountBeforeCut}, After={faceCountAfterCut}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_Extrude_ThroughAll_CreatesFeature()
    {
        // Expected: ThroughAll end condition also creates a feature
        var (sel, sketch, feature) = RealServices();
        OpenRectSketch(sel, sketch);

        var info = feature.Extrude(0.001, EndCondition.ThroughAll);

        Assert.Equal("Extrude", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_Fillet_OnSelectedEdge_CreatesFeature()
    {
        var (sel, sketch, feature) = RealServices();
        OpenRectSketch(sel, sketch);
        feature.Extrude(0.02);

        var firstEdge = sel.ListEntities(SelectableEntityType.Edge).First();
        var selected = sel.SelectEntity(SelectableEntityType.Edge, firstEdge.Index);
        Assert.True(selected.Success, selected.Message);

        var info = feature.Fillet(0.001);

        Assert.Equal("Fillet", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_SimpleHole_OnSelectedFacePoint_CreatesFeature()
    {
        var (sel, sketch, feature) = RealServices();
        OpenRectSketch(sel, sketch);
        feature.Extrude(0.02);

        var firstFace = sel.ListEntities(SelectableEntityType.Face).First();
        var selectedFace = sel.SelectEntity(SelectableEntityType.Face, firstFace.Index);
        Assert.True(selectedFace.Success, selectedFace.Message);

        var point = CreateSketchPointOnSelectedFace();

        sel.ClearSelection();
        selectedFace = sel.SelectEntity(SelectableEntityType.Face, firstFace.Index);
        Assert.True(selectedFace.Success, selectedFace.Message);
        SelectSketchPoint(point);

        var info = feature.SimpleHole(0.005, 0.01);

        Assert.Equal("SimpleHole", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ExtrudeCut_OnSelectedTopFaceSketch_CreatesFeature()
    {
        var (sel, sketch, feature) = RealServices();
        OpenRectSketch(sel, sketch);
        feature.Extrude(0.02);

        SelectTopPlanarFace(sel);
        sketch.InsertSketch();
        sketch.AddCircle(0, 0, 0.005);
        int faceCountBeforeCut = sel.ListEntities(SelectableEntityType.Face).Count;

        var info = feature.ExtrudeCut(0.05, EndCondition.ThroughAll);
        int faceCountAfterCut = sel.ListEntities(SelectableEntityType.Face).Count;

        Assert.Equal("ExtrudeCut", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
        Assert.True(faceCountAfterCut > faceCountBeforeCut,
            $"ExtrudeCut should change solid topology. Before={faceCountBeforeCut}, After={faceCountAfterCut}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ExtrudeCut_AfterFinishSketchOnSelectedTopFace_CreatesFeature()
    {
        var (sel, sketch, feature) = RealServices();
        OpenRectSketch(sel, sketch);
        feature.Extrude(0.02);

        SelectTopPlanarFace(sel);
        sketch.InsertSketch();
        sketch.AddCircle(0, 0, 0.005);
        sketch.FinishSketch();
        int faceCountBeforeCut = sel.ListEntities(SelectableEntityType.Face).Count;

        var info = feature.ExtrudeCut(0.05, EndCondition.ThroughAll);
        int faceCountAfterCut = sel.ListEntities(SelectableEntityType.Face).Count;

        Assert.Equal("ExtrudeCut", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
        Assert.True(faceCountAfterCut > faceCountBeforeCut,
            $"ExtrudeCut should change solid topology. Before={faceCountBeforeCut}, After={faceCountAfterCut}");
    }
}
