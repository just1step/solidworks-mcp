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
}
