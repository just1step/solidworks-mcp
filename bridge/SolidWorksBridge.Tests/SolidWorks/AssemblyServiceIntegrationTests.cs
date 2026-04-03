using SolidWorks.Interop.sldworks;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

/// <summary>
/// Integration tests for AssemblyService.
/// Requires a running SolidWorks instance.
/// Run: dotnet test --filter "Category=Integration"
/// </summary>
[Collection("SolidWorks Integration")]
public class AssemblyServiceIntegrationTests : IDisposable
{
    private readonly SolidWorksIntegrationTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    // ─────────────────────────────────────────────────────────────
    // Integration Tests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_InsertComponent_CreatesComponentInAssembly()
    {
        // Arrange: create+save a part, then open a new assembly
        string partPath = _ctx.CreateAndSaveBoxPart();

        _ctx.Documents.NewDocument(SwDocType.Assembly);

        // Act
        var info = _ctx.Assembly.InsertComponent(partPath, 0, 0, 0);

        // Assert
        Assert.False(string.IsNullOrEmpty(info.Name), "Component should have a name");
        Assert.Equal(partPath, info.Path, StringComparer.OrdinalIgnoreCase);
        // Note: temp part file is left on disk — SolidWorks holds it open while the assembly is open
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ListComponents_ReturnsInsertedComponents()
    {
        string partPath = _ctx.CreateAndSaveBoxPart();

        _ctx.Documents.NewDocument(SwDocType.Assembly);

        _ctx.Assembly.InsertComponent(partPath, 0, 0, 0);

        var components = _ctx.Assembly.ListComponents();

        Assert.NotEmpty(components);
        Assert.Contains(components, c =>
            string.Equals(c.Path, partPath, StringComparison.OrdinalIgnoreCase));
        // Note: temp part file is left on disk — SolidWorks holds it open while the assembly is open
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ListComponents_EmptyAssembly_ReturnsEmpty()
    {
        _ctx.Documents.NewDocument(SwDocType.Assembly);

        var components = _ctx.Assembly.ListComponents();

        Assert.Empty(components);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_CheckInterference_OnOverlappingComponents_ReturnsInterference()
    {
        string partPath = _ctx.CreateAndSaveBoxPart();

        _ctx.Documents.NewDocument(SwDocType.Assembly);

        var first = _ctx.Assembly.InsertComponent(partPath, 0, 0, 0);
        var second = _ctx.Assembly.InsertComponent(partPath, 0, 0, 0);
        var components = _ctx.Assembly.ListComponents();

        Assert.NotEqual(first.Name, second.Name);
        Assert.Equal(2, components.Count);
        Assert.Contains(components, component =>
            string.Equals(component.Name, first.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(components, component =>
            string.Equals(component.Name, second.Name, StringComparison.OrdinalIgnoreCase));

        var result = _ctx.Assembly.CheckInterference([first.Name, second.Name]);

        Assert.True(result.HasInterference, "Expected overlapping components to interfere.");
        Assert.Equal(2, result.CheckedComponentCount);
        Assert.True(result.InterferingFaceCount > 0,
            $"Expected interfering faces to be reported, got {result.InterferingFaceCount}.");
        Assert.Contains(result.InterferingComponents, component =>
            string.Equals(component.HierarchyPath, first.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.InterferingComponents, component =>
            string.Equals(component.HierarchyPath, second.Name, StringComparison.OrdinalIgnoreCase));
    }
}
