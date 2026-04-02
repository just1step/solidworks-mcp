using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

/// <summary>
/// Integration tests for SketchService.
/// Requires a running SolidWorks instance.
/// Each test creates a fresh Part, selects 前视基准面, opens a sketch,
/// draws entities, then closes the sketch.
/// Run: dotnet test --filter "Category=Integration"
/// </summary>
[Collection("SolidWorks Integration")]
public class SketchServiceIntegrationTests : IDisposable
{
    private readonly SolidWorksIntegrationTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    // ─────────────────────────────────────────────────────────────
    // Integration Tests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_InsertAndFinishSketch_DoesNotThrow()
    {
        // Expected: open + close a sketch on 前视基准面 without exception
        _ctx.Documents.NewDocument(SwDocType.Part);
        _ctx.Selection.SelectByName("前视基准面", "PLANE");

        _ctx.Sketch.InsertSketch();
        _ctx.Sketch.FinishSketch();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddPoint_CreatesPointEntity()
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();

        var info = _ctx.Sketch.AddPoint(0.01, 0.02);
        _ctx.Sketch.FinishSketch();

        Assert.Equal("Point", info.Type);
        Assert.Equal(0.01, info.X1, precision: 6);
        Assert.Equal(0.02, info.Y1, precision: 6);
        Assert.Equal(0.01, info.X2, precision: 6);
        Assert.Equal(0.02, info.Y2, precision: 6);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddEllipse_CreatesEllipseEntity()
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();

        var info = _ctx.Sketch.AddEllipse(0, 0, 0.03, 0, 0, 0.01);
        _ctx.Sketch.FinishSketch();

        Assert.Equal("Ellipse", info.Type);
        Assert.Equal(0, info.X1, precision: 6);
        Assert.Equal(0, info.Y1, precision: 6);
        Assert.Equal(0.03, info.X2, precision: 6);
        Assert.Equal(0, info.Y2, precision: 6);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddPolygon_CreatesPolygonEntity()
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();

        var info = _ctx.Sketch.AddPolygon(0, 0, 0.02, 0, 6, true);
        _ctx.Sketch.FinishSketch();

        Assert.Equal("Polygon", info.Type);
        Assert.Equal(0, info.X1, precision: 6);
        Assert.Equal(0, info.Y1, precision: 6);
        Assert.Equal(0.02, info.X2, precision: 6);
        Assert.Equal(0, info.Y2, precision: 6);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddText_CreatesTextEntity()
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();

        var info = _ctx.Sketch.AddText(0.01, 0.02, "HELLO");
        _ctx.Sketch.FinishSketch();

        Assert.Equal("Text", info.Type);
        Assert.Equal(0.01, info.X1, precision: 6);
        Assert.Equal(0.02, info.Y1, precision: 6);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddLine_CreatesLineEntity()
    {
        // Expected: a line from (0,0) to (0.05, 0) returns SketchEntityInfo with correct coords
        _ctx.Documents.NewDocument(SwDocType.Part);
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();

        var info = _ctx.Sketch.AddLine(0, 0, 0.05, 0);
        _ctx.Sketch.FinishSketch();

        Assert.Equal("Line", info.Type);
        Assert.Equal(0, info.X1);
        Assert.Equal(0, info.Y1);
        Assert.Equal(0.05, info.X2, precision: 6);
        Assert.Equal(0, info.Y2, precision: 6);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddCircle_CreatesCircleEntity()
    {
        // Expected: a circle at origin with 0.025m radius
        _ctx.Documents.NewDocument(SwDocType.Part);
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();

        var info = _ctx.Sketch.AddCircle(0, 0, 0.025);
        _ctx.Sketch.FinishSketch();

        Assert.Equal("Circle", info.Type);
        Assert.Equal(0, info.X1, precision: 6); // center x
        Assert.Equal(0, info.Y1, precision: 6); // center y
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddRectangle_CreatesRectangleEntity()
    {
        // Expected: a 100mm x 60mm rectangle centred at origin
        _ctx.Documents.NewDocument(SwDocType.Part);
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();

        var info = _ctx.Sketch.AddRectangle(-0.05, -0.03, 0.05, 0.03);
        _ctx.Sketch.FinishSketch();

        Assert.Equal("Rectangle", info.Type);
        Assert.Equal(-0.05, info.X1, precision: 6);
        Assert.Equal(-0.03, info.Y1, precision: 6);
        Assert.Equal(0.05,  info.X2, precision: 6);
        Assert.Equal(0.03,  info.Y2, precision: 6);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddArc_CreatesArcEntity()
    {
        // Quarter-circle: center (0,0), start (0.025,0), end (0,0.025), CCW
        _ctx.Documents.NewDocument(SwDocType.Part);
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();

        var info = _ctx.Sketch.AddArc(0, 0, 0.025, 0, 0, 0.025, direction: 1);
        _ctx.Sketch.FinishSketch();

        Assert.Equal("Arc", info.Type);
    }
}
