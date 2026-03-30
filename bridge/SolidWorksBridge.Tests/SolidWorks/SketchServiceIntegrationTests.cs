using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

/// <summary>
/// Integration tests for SketchService.
/// Requires a running SolidWorks instance.
/// Each test creates a fresh Part, selects 前视基准面, opens a sketch,
/// draws entities, then closes the sketch.
/// Run: dotnet test --filter "Category=Integration"
/// </summary>
public class SketchServiceIntegrationTests
{
    // ── Setup ─────────────────────────────────────────────────────

    private static (SelectionService sel, SketchService sketch) RealServices()
    {
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);
        manager.Connect();

        var docs = new DocumentService(manager);
        docs.NewDocument(SwDocType.Part);

        return (new SelectionService(manager), new SketchService(manager));
    }

    // ─────────────────────────────────────────────────────────────
    // Integration Tests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_InsertAndFinishSketch_DoesNotThrow()
    {
        // Expected: open + close a sketch on 前视基准面 without exception
        var (sel, sketch) = RealServices();
        sel.SelectByName("前视基准面", "PLANE");

        sketch.InsertSketch();
        sketch.FinishSketch();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddLine_CreatesLineEntity()
    {
        // Expected: a line from (0,0) to (0.05, 0) returns SketchEntityInfo with correct coords
        var (sel, sketch) = RealServices();
        sel.SelectByName("前视基准面", "PLANE");
        sketch.InsertSketch();

        var info = sketch.AddLine(0, 0, 0.05, 0);
        sketch.FinishSketch();

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
        var (sel, sketch) = RealServices();
        sel.SelectByName("前视基准面", "PLANE");
        sketch.InsertSketch();

        var info = sketch.AddCircle(0, 0, 0.025);
        sketch.FinishSketch();

        Assert.Equal("Circle", info.Type);
        Assert.Equal(0, info.X1, precision: 6); // center x
        Assert.Equal(0, info.Y1, precision: 6); // center y
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AddRectangle_CreatesRectangleEntity()
    {
        // Expected: a 100mm x 60mm rectangle centred at origin
        var (sel, sketch) = RealServices();
        sel.SelectByName("前视基准面", "PLANE");
        sketch.InsertSketch();

        var info = sketch.AddRectangle(-0.05, -0.03, 0.05, 0.03);
        sketch.FinishSketch();

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
        var (sel, sketch) = RealServices();
        sel.SelectByName("前视基准面", "PLANE");
        sketch.InsertSketch();

        var info = sketch.AddArc(0, 0, 0.025, 0, 0, 0.025, direction: 1);
        sketch.FinishSketch();

        Assert.Equal("Arc", info.Type);
    }
}
