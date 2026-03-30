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

        var info = feature.ExtrudeCut(0.05, EndCondition.ThroughAll);

        Assert.Equal("ExtrudeCut", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
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
}
