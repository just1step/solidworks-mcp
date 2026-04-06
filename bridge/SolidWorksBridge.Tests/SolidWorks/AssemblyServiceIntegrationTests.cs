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

    private (string PartPath, string SubAssemblyPath, string TopLevelSubAssemblyHierarchyPath, IReadOnlyList<ComponentInstanceInfo> NestedPartInstances)
        CreateNestedAssemblyWithRepeatedPart()
    {
        string partPath = _ctx.CreateAndSaveBoxPart();

        _ctx.Documents.NewDocument(SwDocType.Assembly);
        _ctx.Assembly.InsertComponent(partPath, 0, 0, 0);
        _ctx.Assembly.InsertComponent(partPath, 0.04, 0, 0);
        string subAssemblyPath = _ctx.SaveActiveDocumentAs(".sldasm");

        _ctx.Documents.NewDocument(SwDocType.Assembly);
        var insertedSubAssembly = _ctx.Assembly.InsertComponent(subAssemblyPath, 0, 0, 0);
        var recursiveComponents = _ctx.Assembly.ListComponentsRecursive();
        var nestedPartInstances = recursiveComponents
            .Where(component => string.Equals(component.Path, partPath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(component => component.HierarchyPath, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();

        Assert.Equal(2, nestedPartInstances.Count);

        return (partPath, subAssemblyPath, insertedSubAssembly.Name, nestedPartInstances);
    }

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

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ResolveComponentTarget_WithNestedHierarchyPath_ReturnsOwningSubassemblyAndReuseCount()
    {
        var setup = CreateNestedAssemblyWithRepeatedPart();
        var target = setup.NestedPartInstances[0];
        string expectedOwningHierarchyPath = target.HierarchyPath[..target.HierarchyPath.LastIndexOf('/')];

        var result = _ctx.Assembly.ResolveComponentTarget(hierarchyPath: target.HierarchyPath);

        Assert.True(result.IsResolved);
        Assert.False(result.IsAmbiguous);
        Assert.NotNull(result.ResolvedInstance);
        Assert.Equal(target.HierarchyPath, result.ResolvedInstance!.HierarchyPath);
        Assert.Equal(expectedOwningHierarchyPath, result.OwningAssemblyHierarchyPath);
        Assert.StartsWith(setup.TopLevelSubAssemblyHierarchyPath, result.OwningAssemblyHierarchyPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(setup.SubAssemblyPath, result.OwningAssemblyFilePath, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, result.SourceFileReuseCount);
        Assert.Single(result.MatchingInstances);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ResolveComponentTarget_WithSharedComponentPath_ReturnsAmbiguity()
    {
        var setup = CreateNestedAssemblyWithRepeatedPart();

        var result = _ctx.Assembly.ResolveComponentTarget(componentPath: setup.PartPath);

        Assert.False(result.IsResolved);
        Assert.True(result.IsAmbiguous);
        Assert.Null(result.ResolvedInstance);
        Assert.Equal(2, result.MatchingInstances.Count);
        Assert.All(result.MatchingInstances, instance =>
            Assert.Equal(setup.PartPath, instance.Path, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AnalyzeSharedPartEditImpact_ForNestedReusedPart_ReturnsReplaceRecommendation()
    {
        var setup = CreateNestedAssemblyWithRepeatedPart();
        var target = setup.NestedPartInstances[0];

        var result = _ctx.Assembly.AnalyzeSharedPartEditImpact(hierarchyPath: target.HierarchyPath);

        Assert.True(result.TargetResolution.IsResolved);
        Assert.False(result.SafeDirectEdit);
        Assert.Equal("replace_single_instance_before_edit", result.RecommendedAction);
        Assert.Equal(2, result.AffectedInstanceCount);
        Assert.Equal(setup.PartPath, result.SourceFilePath, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            setup.NestedPartInstances.Select(component => component.HierarchyPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            result.AffectedInstances.Select(component => component.HierarchyPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_AnalyzeSharedPartEditImpact_ForSingleUsePart_ReturnsSafeDirectEdit()
    {
        string partPath = _ctx.CreateAndSaveBoxPart();

        _ctx.Documents.NewDocument(SwDocType.Assembly);
        var insertedComponent = _ctx.Assembly.InsertComponent(partPath, 0, 0, 0);

        var result = _ctx.Assembly.AnalyzeSharedPartEditImpact(hierarchyPath: insertedComponent.Name);

        Assert.True(result.TargetResolution.IsResolved);
        Assert.True(result.SafeDirectEdit);
        Assert.Equal("safe_direct_edit", result.RecommendedAction);
        Assert.Equal(1, result.AffectedInstanceCount);
        Assert.Single(result.AffectedInstances);
        Assert.Equal(insertedComponent.Name, result.AffectedInstances[0].HierarchyPath);
        Assert.Equal(partPath, result.SourceFilePath, StringComparer.OrdinalIgnoreCase);
    }
}
