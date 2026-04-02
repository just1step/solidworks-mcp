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
public class FeatureServiceIntegrationTests : IDisposable
{
    private readonly SolidWorksIntegrationTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    /// <summary>
    /// Helper: select 前视基准面, open a sketch, draw a 100mm x 60mm rectangle.
    /// The sketch is LEFT OPEN (in edit mode) so that Extrude/Cut can immediately
    /// be called — SolidWorks closes the sketch internally during feature creation.
    /// </summary>
    private void OpenRectSketch()
    {
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();
        _ctx.Sketch.AddRectangle(-0.05, -0.03, 0.05, 0.03);
        // NOTE: do NOT call FinishSketch — Extrude will close the sketch internally
    }

    private void SelectTopPlanarFace()
    {
        var topFace = _ctx.Selection.ListEntities(SelectableEntityType.Face)
            .Where(face => face.Box is { Length: >= 6 } box && Math.Abs(box[2] - box[5]) < 1e-9)
            .OrderByDescending(face => face.Box![5])
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No top planar face found on the active solid body.");

        var selected = _ctx.Selection.SelectEntity(SelectableEntityType.Face, topFace.Index);
        Assert.True(selected.Success, selected.Message);
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
        _ctx.Documents.NewDocument(SwDocType.Part);
        OpenRectSketch();

        var info = _ctx.Feature.Extrude(0.02);

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
        _ctx.Documents.NewDocument(SwDocType.Part);

        OpenRectSketch();
        _ctx.Feature.Extrude(0.02);  // box 100x60x20mm

        // Draw a circle on 前视基准面 and extrude-cut through the box
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();
        _ctx.Sketch.AddCircle(0, 0, 0.01);
        // Leave sketch open — ExtrudeCut closes it internally
        int faceCountBeforeCut = _ctx.Selection.ListEntities(SelectableEntityType.Face).Count;

        var info = _ctx.Feature.ExtrudeCut(0.05, EndCondition.ThroughAll);
        int faceCountAfterCut = _ctx.Selection.ListEntities(SelectableEntityType.Face).Count;

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
        _ctx.Documents.NewDocument(SwDocType.Part);
        OpenRectSketch();

        var info = _ctx.Feature.Extrude(0.001, EndCondition.ThroughAll);

        Assert.Equal("Extrude", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_Fillet_OnSelectedEdge_CreatesFeature()
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        OpenRectSketch();
        _ctx.Feature.Extrude(0.02);

        var firstEdge = _ctx.Selection.ListEntities(SelectableEntityType.Edge).First();
        var selected = _ctx.Selection.SelectEntity(SelectableEntityType.Edge, firstEdge.Index);
        Assert.True(selected.Success, selected.Message);

        var info = _ctx.Feature.Fillet(0.001);

        Assert.Equal("Fillet", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ExtrudeCut_OnSelectedTopFaceSketch_CreatesFeature()
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        OpenRectSketch();
        _ctx.Feature.Extrude(0.02);

        SelectTopPlanarFace();
        _ctx.Sketch.InsertSketch();
        _ctx.Sketch.AddCircle(0, 0, 0.005);
        int faceCountBeforeCut = _ctx.Selection.ListEntities(SelectableEntityType.Face).Count;

        var info = _ctx.Feature.ExtrudeCut(0.05, EndCondition.ThroughAll);
        int faceCountAfterCut = _ctx.Selection.ListEntities(SelectableEntityType.Face).Count;

        Assert.Equal("ExtrudeCut", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
        Assert.True(faceCountAfterCut > faceCountBeforeCut,
            $"ExtrudeCut should change solid topology. Before={faceCountBeforeCut}, After={faceCountAfterCut}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ExtrudeCut_AfterFinishSketchOnSelectedTopFace_CreatesFeature()
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        OpenRectSketch();
        _ctx.Feature.Extrude(0.02);

        SelectTopPlanarFace();
        _ctx.Sketch.InsertSketch();
        _ctx.Sketch.AddCircle(0, 0, 0.005);
        _ctx.Sketch.FinishSketch();
        int faceCountBeforeCut = _ctx.Selection.ListEntities(SelectableEntityType.Face).Count;

        var info = _ctx.Feature.ExtrudeCut(0.05, EndCondition.ThroughAll);
        int faceCountAfterCut = _ctx.Selection.ListEntities(SelectableEntityType.Face).Count;

        Assert.Equal("ExtrudeCut", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
        Assert.True(faceCountAfterCut > faceCountBeforeCut,
            $"ExtrudeCut should change solid topology. Before={faceCountBeforeCut}, After={faceCountAfterCut}");
    }
}
