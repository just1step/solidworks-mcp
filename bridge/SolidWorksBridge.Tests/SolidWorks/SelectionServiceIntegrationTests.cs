using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

/// <summary>
/// Integration tests for SelectionService.
/// Requires a running SolidWorks instance with at least one open Part document.
/// Run: dotnet test --filter "Category=Integration"
/// </summary>
[Collection("SolidWorks Integration")]
public class SelectionServiceIntegrationTests : IDisposable
{
    private readonly SolidWorksIntegrationTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    private void CreateExtrudedTestBody()
    {
        _ctx.Selection.SelectByName("前视基准面", "PLANE");
        _ctx.Sketch.InsertSketch();
        _ctx.Sketch.AddRectangle(-0.02, -0.015, 0.02, 0.015);
        _ctx.Feature.Extrude(0.01);
    }

    // ─────────────────────────────────────────────────────────────
    // Integration Tests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_SelectByName_FrontPlane_Succeeds()
    {
        // Expected: "前视基准面" is always present in a new Part doc (Chinese SW)
        _ctx.Documents.NewDocument(SwDocType.Part);

        var result = _ctx.Selection.SelectByName("前视基准面", "PLANE");

        Assert.True(result.Success,
            $"Expected to select front plane but got: {result.Message}");
        Assert.Contains("前视基准面", result.Message);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_SelectByName_TopPlane_Succeeds()
    {
        // Expected: "上视基准面" is always present in a new Part doc (Chinese SW)
        _ctx.Documents.NewDocument(SwDocType.Part);

        var result = _ctx.Selection.SelectByName("上视基准面", "PLANE");

        Assert.True(result.Success,
            $"Expected to select top plane but got: {result.Message}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_SelectByName_NonExistent_ReturnsFalse()
    {
        // Expected: an entity that does not exist returns Success=false, no exception
        _ctx.Documents.NewDocument(SwDocType.Part);

        var result = _ctx.Selection.SelectByName("__does_not_exist__", "PLANE");

        Assert.False(result.Success);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ClearSelection_DoesNotThrow()
    {
        // Expected: ClearSelection after a successful select produces no error
        _ctx.Documents.NewDocument(SwDocType.Part);

        _ctx.Selection.SelectByName("前视基准面", "PLANE");

        // Should complete without exception
        var exception = Record.Exception(() => _ctx.Selection.ClearSelection());
        Assert.Null(exception);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ListEntities_OnSolidBody_ReturnsTopology()
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        CreateExtrudedTestBody();

        var faces = _ctx.Selection.ListEntities(SelectableEntityType.Face);
        var edges = _ctx.Selection.ListEntities(SelectableEntityType.Edge);
        var vertices = _ctx.Selection.ListEntities(SelectableEntityType.Vertex);

        Assert.NotEmpty(faces);
        Assert.NotEmpty(edges);
        Assert.NotEmpty(vertices);
        Assert.All(faces, face => Assert.Equal(SelectableEntityType.Face, face.EntityType));
        Assert.All(edges, edge => Assert.Equal(SelectableEntityType.Edge, edge.EntityType));
        Assert.All(vertices, vertex => Assert.Equal(SelectableEntityType.Vertex, vertex.EntityType));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_SelectEntity_FromListedFace_Succeeds()
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        CreateExtrudedTestBody();

        var target = _ctx.Selection.ListEntities(SelectableEntityType.Face).First();
        var result = _ctx.Selection.SelectEntity(SelectableEntityType.Face, target.Index);

        Assert.True(result.Success, result.Message);
    }
}
