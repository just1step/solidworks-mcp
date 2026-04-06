using Moq;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

public class WorkflowServiceTests
{
    private static SwApiDiagnostics Diagnostics() => new(0, Array.Empty<SwCodeInfo>(), 0, Array.Empty<SwCodeInfo>());

    private static AssemblyTargetResolutionResult ResolvedTarget(
        string hierarchyPath = "SubAsm-1/Pulley-1",
        string sourcePath = @"C:\OldPulley.sldprt",
        string owningHierarchyPath = "SubAsm-1",
        string owningAssemblyFilePath = @"C:\SubAsm.sldasm",
        int depth = 1,
        int reuseCount = 2)
    {
        var resolvedInstance = new ComponentInstanceInfo("Pulley-1", sourcePath, hierarchyPath, depth);
        return new AssemblyTargetResolutionResult(
            RequestedName: null,
            RequestedHierarchyPath: hierarchyPath,
            RequestedComponentPath: null,
            IsResolved: true,
            IsAmbiguous: false,
            ResolvedInstance: resolvedInstance,
            OwningAssemblyHierarchyPath: owningHierarchyPath,
            OwningAssemblyFilePath: owningAssemblyFilePath,
            SourceFileReuseCount: reuseCount,
            MatchingInstances: new[] { resolvedInstance });
    }

    private static SharedPartEditImpactResult Impact(
        AssemblyTargetResolutionResult resolution,
        string sourcePath,
        int affectedCount,
        bool safeDirectEdit,
        params string[] hierarchyPaths)
    {
        var instances = hierarchyPaths
            .Select((path, index) => new ComponentInstanceInfo($"Instance-{index + 1}", sourcePath, path, path.Count(c => c == '/')))
            .ToArray();
        return new SharedPartEditImpactResult(
            resolution,
            sourcePath,
            affectedCount,
            instances,
            safeDirectEdit,
            safeDirectEdit ? "safe_direct_edit" : "replace_single_instance_before_edit");
    }

    [Fact]
    public void ReviewTargetedStaticInterference_WhenSecondTargetIsMissing_ReturnsFailureWithoutRunningCheck()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var firstResolution = ResolvedTarget(hierarchyPath: "SubAsm-1/Pulley-1", sourcePath: @"C:\Pulley.sldprt", reuseCount: 1);
        var missingResolution = new AssemblyTargetResolutionResult(
            RequestedName: null,
            RequestedHierarchyPath: "SubAsm-1/Missing-1",
            RequestedComponentPath: null,
            IsResolved: false,
            IsAmbiguous: false,
            ResolvedInstance: null,
            OwningAssemblyHierarchyPath: null,
            OwningAssemblyFilePath: null,
            SourceFileReuseCount: 0,
            MatchingInstances: Array.Empty<ComponentInstanceInfo>());

        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Pulley-1", null)).Returns(firstResolution);
        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Missing-1", null)).Returns(missingResolution);

        var service = new WorkflowService(documents.Object, assembly.Object);

        var result = service.ReviewTargetedStaticInterference("SubAsm-1/Pulley-1", "SubAsm-1/Missing-1");

        Assert.Equal("second_target_not_resolved", result.Status);
        Assert.False(result.ScopeValidated);
        Assert.False(result.ScopeEvaluatedAsRequested);
        Assert.False(result.HasInterference);
        Assert.Null(result.InterferenceCheck);
        assembly.Verify(a => a.CheckInterference(It.IsAny<IReadOnlyList<string>>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void ReviewTargetedStaticInterference_WhenCheckScopeIsShort_ReturnsInvalidScope()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var firstResolution = ResolvedTarget(hierarchyPath: "SubAsm-1/Pulley-1", sourcePath: @"C:\Pulley.sldprt", reuseCount: 1);
        var secondResolution = ResolvedTarget(hierarchyPath: "SubAsm-1/Bracket-1", sourcePath: @"C:\Bracket.sldprt", reuseCount: 1);

        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Pulley-1", null)).Returns(firstResolution);
        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Bracket-1", null)).Returns(secondResolution);
        assembly.Setup(a => a.CheckInterference(It.Is<IReadOnlyList<string>>(paths => paths.Count == 2), false))
            .Returns(new AssemblyInterferenceCheckResult(
                HasInterference: false,
                TreatCoincidenceAsInterference: false,
                CheckedComponentCount: 1,
                InterferingFaceCount: 0,
                InterferingComponents: Array.Empty<ComponentInstanceInfo>()));

        var service = new WorkflowService(documents.Object, assembly.Object);

        var result = service.ReviewTargetedStaticInterference("SubAsm-1/Pulley-1", "SubAsm-1/Bracket-1");

        Assert.Equal("scope_not_evaluated_as_requested", result.Status);
        Assert.True(result.ScopeValidated);
        Assert.False(result.ScopeEvaluatedAsRequested);
        Assert.NotNull(result.InterferenceCheck);
    }

    [Fact]
    public void ReviewTargetedStaticInterference_CompletesForKnownInterferingPair()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var firstResolution = ResolvedTarget(hierarchyPath: "SubAsm-1/Pulley-1", sourcePath: @"C:\Pulley.sldprt", reuseCount: 1);
        var secondResolution = ResolvedTarget(hierarchyPath: "SubAsm-1/Bracket-1", sourcePath: @"C:\Bracket.sldprt", reuseCount: 1);

        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Pulley-1", null)).Returns(firstResolution);
        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Bracket-1", null)).Returns(secondResolution);
        assembly.Setup(a => a.CheckInterference(It.Is<IReadOnlyList<string>>(paths =>
                paths.SequenceEqual(new[] { "SubAsm-1/Pulley-1", "SubAsm-1/Bracket-1" })), false))
            .Returns(new AssemblyInterferenceCheckResult(
                HasInterference: true,
                TreatCoincidenceAsInterference: false,
                CheckedComponentCount: 2,
                InterferingFaceCount: 2,
                InterferingComponents: new[]
                {
                    new ComponentInstanceInfo("Pulley-1", @"C:\Pulley.sldprt", "SubAsm-1/Pulley-1", 1),
                    new ComponentInstanceInfo("Bracket-1", @"C:\Bracket.sldprt", "SubAsm-1/Bracket-1", 1),
                }));

        var service = new WorkflowService(documents.Object, assembly.Object);

        var result = service.ReviewTargetedStaticInterference("SubAsm-1/Pulley-1", "SubAsm-1/Bracket-1");

        Assert.Equal("completed", result.Status);
        Assert.True(result.ScopeValidated);
        Assert.True(result.ScopeEvaluatedAsRequested);
        Assert.True(result.HasInterference);
        Assert.NotNull(result.InterferenceCheck);
        Assert.Equal(2, result.InterferenceCheck!.CheckedComponentCount);
        Assert.Equal(2, result.InterferenceCheck.InterferingComponents.Count);
    }

    [Fact]
    public void ReviewTargetedStaticInterference_CompletesForKnownNonInterferingPair()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var firstResolution = ResolvedTarget(hierarchyPath: "SubAsm-1/Pulley-1", sourcePath: @"C:\Pulley.sldprt", reuseCount: 1);
        var secondResolution = ResolvedTarget(hierarchyPath: "SubAsm-1/Bracket-1", sourcePath: @"C:\Bracket.sldprt", reuseCount: 1);

        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Pulley-1", null)).Returns(firstResolution);
        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Bracket-1", null)).Returns(secondResolution);
        assembly.Setup(a => a.CheckInterference(It.IsAny<IReadOnlyList<string>>(), false))
            .Returns(new AssemblyInterferenceCheckResult(
                HasInterference: false,
                TreatCoincidenceAsInterference: false,
                CheckedComponentCount: 2,
                InterferingFaceCount: 0,
                InterferingComponents: Array.Empty<ComponentInstanceInfo>()));

        var service = new WorkflowService(documents.Object, assembly.Object);

        var result = service.ReviewTargetedStaticInterference("SubAsm-1/Pulley-1", "SubAsm-1/Bracket-1");

        Assert.Equal("completed", result.Status);
        Assert.True(result.ScopeValidated);
        Assert.True(result.ScopeEvaluatedAsRequested);
        Assert.False(result.HasInterference);
        Assert.NotNull(result.InterferenceCheck);
        Assert.Empty(result.InterferenceCheck!.InterferingComponents);
    }

    [Fact]
    public void ReplaceNestedComponentAndVerifyPersistence_WhenTargetIsAmbiguous_ReturnsFailureWithoutMutation()
    {
        const string replacementFilePath = @"D:\Temp\NewPulley.sldprt";
        Directory.CreateDirectory(Path.GetDirectoryName(replacementFilePath)!);
        File.WriteAllText(replacementFilePath, "placeholder");

        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        documents.Setup(d => d.GetActiveDocument()).Returns(new SwDocumentInfo(@"C:\Top.sldasm", "Top", 2));

        var ambiguous = new AssemblyTargetResolutionResult(
            RequestedName: null,
            RequestedHierarchyPath: "SubAsm-1/Pulley-1",
            RequestedComponentPath: null,
            IsResolved: false,
            IsAmbiguous: true,
            ResolvedInstance: null,
            OwningAssemblyHierarchyPath: null,
            OwningAssemblyFilePath: null,
            SourceFileReuseCount: 0,
            MatchingInstances: new[]
            {
                new ComponentInstanceInfo("Pulley-1", @"C:\Pulley.sldprt", "SubAsm-1/Pulley-1", 1),
                new ComponentInstanceInfo("Pulley-1", @"C:\Pulley.sldprt", "SubAsm-2/Pulley-1", 1),
            });
        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Pulley-1", null)).Returns(ambiguous);
        assembly.Setup(a => a.AnalyzeSharedPartEditImpact(null, "SubAsm-1/Pulley-1", null)).Returns(new SharedPartEditImpactResult(
            ambiguous,
            null,
            0,
            Array.Empty<ComponentInstanceInfo>(),
            false,
            "replace_single_instance_before_edit"));

        var service = new WorkflowService(documents.Object, assembly.Object);

        var result = service.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, hierarchyPath: "SubAsm-1/Pulley-1");

        Assert.Equal("target_ambiguous", result.Status);
        Assert.False(result.PersistenceVerified);
        Assert.Null(result.ReplacementResult);
        documents.Verify(d => d.OpenDocument(It.IsAny<string>()), Times.Never);
        documents.Verify(d => d.SaveDocument(It.IsAny<string>()), Times.Never);
        assembly.Verify(a => a.ReplaceComponent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void ReplaceNestedComponentAndVerifyPersistence_WhenTargetIsTopLevel_ReturnsFailureWithoutMutation()
    {
        const string replacementFilePath = @"D:\Temp\BracketNew.sldprt";
        Directory.CreateDirectory(Path.GetDirectoryName(replacementFilePath)!);
        File.WriteAllText(replacementFilePath, "placeholder");

        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        documents.Setup(d => d.GetActiveDocument()).Returns(new SwDocumentInfo(@"C:\Top.sldasm", "Top", 2));

        var resolution = ResolvedTarget(hierarchyPath: "Bracket-1", sourcePath: @"C:\Bracket.sldprt", owningHierarchyPath: null!, owningAssemblyFilePath: null!, depth: 0, reuseCount: 1) with
        {
            OwningAssemblyHierarchyPath = null,
            OwningAssemblyFilePath = null,
        };
        assembly.Setup(a => a.ResolveComponentTarget(null, "Bracket-1", null)).Returns(resolution);
        assembly.Setup(a => a.AnalyzeSharedPartEditImpact(null, "Bracket-1", null)).Returns(Impact(resolution, @"C:\Bracket.sldprt", 1, true, "Bracket-1"));

        var service = new WorkflowService(documents.Object, assembly.Object);

        var result = service.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, hierarchyPath: "Bracket-1");

        Assert.Equal("target_not_nested", result.Status);
        Assert.False(result.PersistenceVerified);
        documents.Verify(d => d.OpenDocument(It.IsAny<string>()), Times.Never);
        assembly.Verify(a => a.ReplaceComponent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void ReplaceNestedComponentAndVerifyPersistence_CompletesAndVerifiesPersistence()
    {
        const string replacementFilePath = @"D:\Temp\ReplacementPulley.sldprt";
        Directory.CreateDirectory(Path.GetDirectoryName(replacementFilePath)!);
        File.WriteAllText(replacementFilePath, "placeholder");

        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var initialResolution = ResolvedTarget();
        var persistedResolution = initialResolution with
        {
            ResolvedInstance = new ComponentInstanceInfo("Pulley-1", replacementFilePath, "SubAsm-1/Pulley-1", 1),
            SourceFileReuseCount = 1,
        };

        documents.SetupSequence(d => d.GetActiveDocument())
            .Returns(new SwDocumentInfo(@"C:\Top.sldasm", "Top", 2))
            .Returns(new SwDocumentInfo(@"C:\SubAsm.sldasm", "SubAsm", 2))
            .Returns(new SwDocumentInfo(@"C:\Top.sldasm", "Top", 2));
        documents.Setup(d => d.OpenDocument(@"C:\SubAsm.sldasm")).Returns(new SwOpenResult(new SwDocumentInfo(@"C:\SubAsm.sldasm", "SubAsm", 2), Diagnostics()));
        documents.Setup(d => d.SaveDocument(@"C:\SubAsm.sldasm")).Returns(new SwSaveResult(@"C:\SubAsm.sldasm", @"C:\SubAsm.sldasm", "sldasm", false, 0, 0, Diagnostics()));
        documents.Setup(d => d.OpenDocument(@"C:\Top.sldasm")).Returns(new SwOpenResult(new SwDocumentInfo(@"C:\Top.sldasm", "Top", 2), Diagnostics()));

        assembly.SetupSequence(a => a.ResolveComponentTarget(null, "SubAsm-1/Pulley-1", null))
            .Returns(initialResolution)
            .Returns(persistedResolution);
        assembly.Setup(a => a.AnalyzeSharedPartEditImpact(null, "SubAsm-1/Pulley-1", null))
            .Returns(Impact(initialResolution, @"C:\OldPulley.sldprt", 2, false, "SubAsm-1/Pulley-1", "SubAsm-1/Pulley-2"));
        assembly.Setup(a => a.AnalyzeSharedPartEditImpact(null, "SubAsm-1/ReplacementPulley-1", null))
            .Returns(Impact(persistedResolution, replacementFilePath, 1, true, "SubAsm-1/ReplacementPulley-1"));
        assembly.Setup(a => a.ReplaceComponent("Pulley-1", replacementFilePath, "", false, 0, true))
            .Returns(new AssemblyComponentReplacementResult("Pulley-1", replacementFilePath, "", false, 0, true, true));
        assembly.Setup(a => a.ListComponentsRecursive()).Returns(new[]
        {
            new ComponentInstanceInfo("ReplacementPulley-1", replacementFilePath, "SubAsm-1/ReplacementPulley-1", 1),
            new ComponentInstanceInfo("Pulley-2", @"C:\OldPulley.sldprt", "SubAsm-1/Pulley-2", 1),
        });

        var service = new WorkflowService(documents.Object, assembly.Object);

        var result = service.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, hierarchyPath: "SubAsm-1/Pulley-1");

        Assert.Equal("completed", result.Status);
        Assert.True(result.OwningAssemblyActivated);
        Assert.True(result.ParentAssemblyReloaded);
        Assert.True(result.PersistenceVerified);
        Assert.NotNull(result.ReplacementResult);
        Assert.Equal("Pulley-1", result.ReplacementTargetHierarchyPath);
        Assert.NotNull(result.PostReplacementImpactAnalysis);
        Assert.True(result.PostReplacementImpactAnalysis!.SafeDirectEdit);
        Assert.Equal(1, result.PostReplacementImpactAnalysis.AffectedInstanceCount);
        Assert.Equal("SubAsm-1/ReplacementPulley-1", result.PersistenceResolution!.ResolvedInstance!.HierarchyPath);
        documents.Verify(d => d.CloseDocument(@"C:\Top.sldasm"), Times.Once);
        documents.Verify(d => d.CloseDocument(@"C:\SubAsm.sldasm"), Times.Once);
        assembly.Verify(a => a.ReplaceComponent("Pulley-1", replacementFilePath, "", false, 0, true), Times.Once);
    }
}