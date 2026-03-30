using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

/// <summary>
/// Integration tests for SelectionService.
/// Requires a running SolidWorks instance with at least one open Part document.
/// Run: dotnet test --filter "Category=Integration"
/// </summary>
[Collection("SolidWorks Integration")]
public class SelectionServiceIntegrationTests
{
    // ── Setup helpers ─────────────────────────────────────────────

    /// <summary>
    /// Create a real SelectionService connected to the running SolidWorks;
    /// also create a fresh Part document so the standard planes exist.
    /// Returns both services so callers can open/close docs as needed.
    /// </summary>
    private static (SelectionService sel, DocumentService docs) RealServices()
    {
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);
        manager.Connect();

        var docs = new DocumentService(manager);
        var sel = new SelectionService(manager);

        // Ensure we have a Part open (standard planes are always present)
        docs.NewDocument(SwDocType.Part);

        return (sel, docs);
    }

    // ─────────────────────────────────────────────────────────────
    // Integration Tests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_SelectByName_FrontPlane_Succeeds()
    {
        // Expected: "前视基准面" is always present in a new Part doc (Chinese SW)
        var (sel, _) = RealServices();

        var result = sel.SelectByName("前视基准面", "PLANE");

        Assert.True(result.Success,
            $"Expected to select front plane but got: {result.Message}");
        Assert.Contains("前视基准面", result.Message);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_SelectByName_TopPlane_Succeeds()
    {
        // Expected: "上视基准面" is always present in a new Part doc (Chinese SW)
        var (sel, _) = RealServices();

        var result = sel.SelectByName("上视基准面", "PLANE");

        Assert.True(result.Success,
            $"Expected to select top plane but got: {result.Message}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_SelectByName_NonExistent_ReturnsFalse()
    {
        // Expected: an entity that does not exist returns Success=false, no exception
        var (sel, _) = RealServices();

        var result = sel.SelectByName("__does_not_exist__", "PLANE");

        Assert.False(result.Success);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ClearSelection_DoesNotThrow()
    {
        // Expected: ClearSelection after a successful select produces no error
        var (sel, _) = RealServices();

        sel.SelectByName("前视基准面", "PLANE");

        // Should complete without exception
        var exception = Record.Exception(() => sel.ClearSelection());
        Assert.Null(exception);
    }
}
