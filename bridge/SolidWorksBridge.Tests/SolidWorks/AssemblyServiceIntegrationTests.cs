using SolidWorks.Interop.sldworks;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

/// <summary>
/// Integration tests for AssemblyService.
/// Requires a running SolidWorks instance.
/// Run: dotnet test --filter "Category=Integration"
/// </summary>
[Collection("SolidWorks Integration")]
public class AssemblyServiceIntegrationTests
{
    // ── Setup ─────────────────────────────────────────────────────

    private static (DocumentService docs, SelectionService sel, AssemblyService assy)
        RealServices()
    {
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);
        manager.Connect();

        return (
            new DocumentService(manager),
            new SelectionService(manager),
            new AssemblyService(manager)
        );
    }

    /// <summary>
    /// Creates a minimal part, saves it to a temp path.
    /// Returns the saved file path.
    /// </summary>
    private static string CreateAndSavePart()
    {
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);
        manager.Connect();

        var docs = new DocumentService(manager);
        docs.NewDocument(SwDocType.Part);

        // Draw a tiny box so the part isn't empty
        var sel = new SelectionService(manager);
        var sketch = new SketchService(manager);
        var feature = new FeatureService(manager);

        sel.SelectByName("前视基准面", "PLANE");
        sketch.InsertSketch();
        sketch.AddRectangle(-0.01, -0.01, 0.01, 0.01);
        feature.Extrude(0.005);

        // Save to temp path
        string path = Path.Combine(Path.GetTempPath(), $"SwTestPart_{Guid.NewGuid():N}.sldprt");
        var swDoc = (IModelDoc2)manager.SwApp!.IActiveDoc2!;
        swDoc.SaveAs3(path, 0, 0);
        return path;
    }

    // ─────────────────────────────────────────────────────────────
    // Integration Tests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_InsertComponent_CreatesComponentInAssembly()
    {
        // Arrange: create+save a part, then open a new assembly
        string partPath = CreateAndSavePart();

        var (docs, _, assy) = RealServices();
        docs.NewDocument(SwDocType.Assembly);

        // Act
        var info = assy.InsertComponent(partPath, 0, 0, 0);

        // Assert
        Assert.False(string.IsNullOrEmpty(info.Name), "Component should have a name");
        Assert.Equal(partPath, info.Path, StringComparer.OrdinalIgnoreCase);
        // Note: temp part file is left on disk — SolidWorks holds it open while the assembly is open
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ListComponents_ReturnsInsertedComponents()
    {
        string partPath = CreateAndSavePart();

        var (docs, _, assy) = RealServices();
        docs.NewDocument(SwDocType.Assembly);

        assy.InsertComponent(partPath, 0, 0, 0);

        var components = assy.ListComponents();

        Assert.NotEmpty(components);
        Assert.Contains(components, c =>
            string.Equals(c.Path, partPath, StringComparison.OrdinalIgnoreCase));
        // Note: temp part file is left on disk — SolidWorks holds it open while the assembly is open
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ListComponents_EmptyAssembly_ReturnsEmpty()
    {
        var (docs, _, assy) = RealServices();
        docs.NewDocument(SwDocType.Assembly);

        var components = assy.ListComponents();

        Assert.Empty(components);
    }
}
