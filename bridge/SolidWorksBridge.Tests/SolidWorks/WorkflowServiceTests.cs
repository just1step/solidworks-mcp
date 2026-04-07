using Moq;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

public class WorkflowServiceTests
{
    private static SwApiDiagnostics Diagnostics() => new(0, Array.Empty<SwCodeInfo>(), 0, Array.Empty<SwCodeInfo>());

    private static void AssertStageLogged(
        RecordingWorkflowStageLogger logger,
        string workflowName,
        string stageName,
        string boundary,
        string? detailContains = null)
    {
        Assert.Contains(logger.Events, entry =>
            string.Equals(entry.WorkflowName, workflowName, StringComparison.Ordinal)
            && string.Equals(entry.StageName, stageName, StringComparison.Ordinal)
            && string.Equals(entry.Boundary, boundary, StringComparison.Ordinal)
            && (detailContains == null || (entry.Detail?.Contains(detailContains, StringComparison.OrdinalIgnoreCase) ?? false)));
    }

    private static FeatureDiagnosticsResult FeatureDiagnostics(int errors = 0, int warnings = 0) =>
        new(
            Array.Empty<FeatureDiagnosticInfo>(),
            Array.Empty<WhatsWrongItemInfo>(),
            errors,
            warnings);

    private static FeatureDiagnosticsResult FeatureDiagnosticsWithCorrelatedIssues(params CorrelatedDiagnosticIssueInfo[] issues) =>
        new(
            Array.Empty<FeatureDiagnosticInfo>(),
            Array.Empty<WhatsWrongItemInfo>(),
            issues.Count(static issue => !issue.IsWarning),
            issues.Count(static issue => issue.IsWarning),
            issues);

    private static DiagnosticTargetContextInfo DiagnosticTargetContext(
        string scopeType,
        string? hierarchyPath = null,
        string? componentName = null,
        string? sourceFilePath = null,
        string? documentPath = @"C:\Asm.sldasm",
        string? owningAssemblyHierarchyPath = null,
        string? owningAssemblyFilePath = @"C:\Asm.sldasm",
        bool isExact = true,
        bool isAmbiguous = false,
        int sourceFileReuseCount = 1,
        string? reason = null,
        params ComponentInstanceInfo[] matchingInstances) =>
        new(
            ScopeType: scopeType,
            IsExact: isExact,
            IsAmbiguous: isAmbiguous,
            DocumentPath: documentPath,
            HierarchyPath: hierarchyPath,
            ComponentName: componentName,
            SourceFilePath: sourceFilePath,
            OwningAssemblyHierarchyPath: owningAssemblyHierarchyPath,
            OwningAssemblyFilePath: owningAssemblyFilePath,
            SourceFileReuseCount: sourceFileReuseCount,
            Reason: reason,
            MatchingInstances: matchingInstances);

    private static CorrelatedDiagnosticIssueInfo CorrelatedIssue(
        string name,
        string typeName,
        int errorCode,
        bool isWarning,
        DiagnosticTargetContextInfo targetContext,
        string errorName = "diagnostic_code",
        string errorDescription = "diagnostic description") =>
        new(
            Source: "feature_tree",
            Index: null,
            Name: name,
            TypeName: typeName,
            ErrorCode: errorCode,
            IsWarning: isWarning,
            ErrorName: errorName,
            ErrorDescription: errorDescription,
            AppearsInWhatsWrong: true,
            TargetContext: targetContext);

    private static RebuildStateInfo RebuildState(int rawStatus) =>
        new(
            rawStatus,
            rawStatus != 0,
            rawStatus == 0
                ? [new SwCodeInfo(0, "swModelRebuildStatus_FullyRebuilt", "The model does not currently need rebuild.")]
                : [new SwCodeInfo(rawStatus, "swModelRebuildStatus_NonFrozenFeatureNeedsRebuild", "Non-frozen features need rebuild.")],
            rawStatus == 0 ? "The model does not currently need rebuild." : "Non-frozen features need rebuild.");

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

    private static SolidWorksCompatibilityInfo Compatibility(
        string compatibilityState,
        string summary,
        int? runtimeMarketingYear = 2025,
        string runtimeRevisionNumber = "33.1.0",
        string licenseName = "swLicenseType_e.swLicenseStandard",
        params string[] notices)
    {
        int? revisionMajor = runtimeMarketingYear.HasValue
            ? runtimeMarketingYear.Value - 1992
            : null;

        return new SolidWorksCompatibilityInfo(
            compatibilityState,
            summary,
            "32.0.0.76",
            32,
            2024,
            new SolidWorksRuntimeVersionInfo(
                runtimeRevisionNumber,
                revisionMajor,
                1,
                0,
                runtimeMarketingYear,
                new SwBuildNumbers("32.1.0", runtimeRevisionNumber, "0"),
                @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\sldworks.exe"),
            new SolidWorksLicenseInfo(1, licenseName, "Test license"),
            notices.Length == 0 ? new[] { "Compatibility notice." } : notices);
    }

    private static string TempFilePath(string fileName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "solidworks-mcp-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName);
    }

    [Fact]
    public void ReviewTargetedStaticInterference_WhenSecondTargetIsMissing_ReturnsFailureWithoutRunningCheck()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var logger = new RecordingWorkflowStageLogger();
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

        var service = new WorkflowService(documents.Object, assembly.Object, null, null, logger);

        var result = service.ReviewTargetedStaticInterference("SubAsm-1/Pulley-1", "SubAsm-1/Missing-1");

        Assert.Equal("second_target_not_resolved", result.Status);
        Assert.False(result.ScopeValidated);
        Assert.False(result.ScopeEvaluatedAsRequested);
        Assert.False(result.HasInterference);
        Assert.Null(result.InterferenceCheck);
        AssertStageLogged(logger, nameof(WorkflowService.ReviewTargetedStaticInterference), "preconditions.first_target_resolution", "completed", "SubAsm-1/Pulley-1");
        AssertStageLogged(logger, nameof(WorkflowService.ReviewTargetedStaticInterference), "preconditions.second_target_resolution", "failed", "second_target_not_resolved");
        AssertStageLogged(logger, nameof(WorkflowService.ReviewTargetedStaticInterference), "final", "failed", "status=second_target_not_resolved");
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
    public void ReviewTargetedStaticInterference_OnCertifiedBaseline_DoesNotAttachAdvisory()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var connectionManager = new Mock<ISwConnectionManager>();
        var firstResolution = ResolvedTarget(hierarchyPath: "SubAsm-1/Pulley-1", sourcePath: @"C:\Pulley.sldprt", reuseCount: 1);
        var secondResolution = ResolvedTarget(hierarchyPath: "SubAsm-1/Bracket-1", sourcePath: @"C:\Bracket.sldprt", reuseCount: 1);

        connectionManager.Setup(c => c.GetCompatibilityInfo())
            .Returns(Compatibility("certified-baseline", "Certified on the interop baseline."));
        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Pulley-1", null)).Returns(firstResolution);
        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Bracket-1", null)).Returns(secondResolution);
        assembly.Setup(a => a.CheckInterference(It.IsAny<IReadOnlyList<string>>(), false))
            .Returns(new AssemblyInterferenceCheckResult(
                HasInterference: false,
                TreatCoincidenceAsInterference: false,
                CheckedComponentCount: 2,
                InterferingFaceCount: 0,
                InterferingComponents: Array.Empty<ComponentInstanceInfo>()));

        var service = new WorkflowService(documents.Object, assembly.Object, null, connectionManager.Object);

        var result = service.ReviewTargetedStaticInterference("SubAsm-1/Pulley-1", "SubAsm-1/Bracket-1");

        Assert.Equal("completed", result.Status);
        Assert.Null(result.CompatibilityAdvisory);
    }

    [Fact]
    public void ReplaceNestedComponentAndVerifyPersistence_WhenTargetIsAmbiguous_ReturnsFailureWithoutMutation()
    {
        var replacementFilePath = TempFilePath("NewPulley.sldprt");
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
    public void ReplaceNestedComponentAndVerifyPersistence_OnUnsupportedNewerVersion_BlocksBeforeMutation()
    {
        var replacementFilePath = TempFilePath("BlockedPulley.sldprt");
        File.WriteAllText(replacementFilePath, "placeholder");

        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var connectionManager = new Mock<ISwConnectionManager>();
        var resolution = ResolvedTarget();
        var impact = Impact(resolution, @"C:\OldPulley.sldprt", 2, false, "SubAsm-1/Pulley-1", "SubAsm-1/Pulley-2");

        connectionManager.Setup(c => c.GetCompatibilityInfo())
            .Returns(Compatibility(
                "unsupported-newer-version",
                "Runtime is newer than the bridge has been validated for.",
                2026,
                "34.0.0",
                "swLicenseType_e.swLicenseStandard",
                "This runtime is newer than the compiled interop baseline and outside the planned certification window."));
        documents.Setup(d => d.GetActiveDocument()).Returns(new SwDocumentInfo(@"C:\Top.sldasm", "Top", 2));
        assembly.Setup(a => a.ResolveComponentTarget(null, "SubAsm-1/Pulley-1", null)).Returns(resolution);
        assembly.Setup(a => a.AnalyzeSharedPartEditImpact(null, "SubAsm-1/Pulley-1", null)).Returns(impact);

        var service = new WorkflowService(documents.Object, assembly.Object, null, connectionManager.Object);

        var result = service.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, hierarchyPath: "SubAsm-1/Pulley-1");

        Assert.Equal("compatibility_state_blocks_operation", result.Status);
        Assert.False(result.PersistenceVerified);
        Assert.NotNull(result.CompatibilityAdvisory);
        Assert.Equal("unsupported-newer-version", result.CompatibilityAdvisory!.CompatibilityState);
        Assert.Contains("not trusted for high-risk mutation workflows", result.FailureReason);
        documents.Verify(d => d.OpenDocument(It.IsAny<string>()), Times.Never);
        documents.Verify(d => d.SaveDocument(It.IsAny<string>()), Times.Never);
        assembly.Verify(a => a.ReplaceComponent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }


    [Fact]
    public void DiagnoseActiveDocumentHealth_WhenEditing_ReturnsBlockedStatus()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var selection = new Mock<ISelectionService>();
        var logger = new RecordingWorkflowStageLogger();
        documents.Setup(d => d.GetActiveDocument()).Returns(new SwDocumentInfo(@"C:\Part.sldprt", "Part", 1));
        selection.Setup(s => s.GetEditState()).Returns(new EditStateInfo(true, "Sketch", false, false));

        var service = new WorkflowService(documents.Object, assembly.Object, selection.Object, null, logger);

        var result = service.DiagnoseActiveDocumentHealth();

        Assert.Equal("editing_state_blocks_diagnostics", result.Status);
        Assert.False(result.ReadyForVerificationGate);
        Assert.True(result.HasBlockingIssues);
        AssertStageLogged(logger, nameof(WorkflowService.DiagnoseActiveDocumentHealth), "preconditions.active_document", "completed", "C:\\Part.sldprt");
        AssertStageLogged(logger, nameof(WorkflowService.DiagnoseActiveDocumentHealth), "preconditions.edit_state", "failed", "editing_state_blocks_diagnostics");
        AssertStageLogged(logger, nameof(WorkflowService.DiagnoseActiveDocumentHealth), "final", "failed", "status=editing_state_blocks_diagnostics");
        selection.Verify(s => s.GetFeatureDiagnostics(), Times.Never);
        documents.Verify(d => d.ForceRebuildActiveDocument(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void DiagnoseActiveDocumentHealth_CollectsRebuildAndSaveDiagnostics()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var selection = new Mock<ISelectionService>();
        var active = new SwDocumentInfo(@"C:\Asm.sldasm", "Asm", 2);
        documents.Setup(d => d.GetActiveDocument()).Returns(active);
        selection.Setup(s => s.GetEditState()).Returns(new EditStateInfo(false, "None", true, true));
        selection.SetupSequence(s => s.GetFeatureDiagnostics())
            .Returns(FeatureDiagnostics())
            .Returns(FeatureDiagnostics(warnings: 1));
        documents.Setup(d => d.ForceRebuildActiveDocument(false)).Returns(new RebuildExecutionResult(true, true, false, RebuildState(1), RebuildState(0)));
        documents.Setup(d => d.SaveDocument(active.Path)).Returns(new SwSaveResult(active.Path, active.Path, "sldasm", false, 0, 1, Diagnostics()));

        var service = new WorkflowService(documents.Object, assembly.Object, selection.Object);

        var result = service.DiagnoseActiveDocumentHealth(forceRebuild: true, topOnly: false, saveDocument: true);

        Assert.Equal("completed", result.Status);
        Assert.False(result.HasBlockingIssues);
        Assert.True(result.HasWarnings);
        Assert.True(result.ReadyForVerificationGate);
        Assert.True(result.Rebuild.RebuildAttempted);
        Assert.True(result.SaveHealth.SaveAttempted);
        Assert.True(result.SaveHealth.SaveSucceeded);
        Assert.True(result.SaveHealth.HasWarnings);
        Assert.NotNull(result.ActionableDiagnostics);
        Assert.Empty(result.ActionableDiagnostics!.CurrentIssues);
        Assert.Empty(result.ActionableDiagnostics.ResolvedByRebuildIssues);
        documents.Verify(d => d.ForceRebuildActiveDocument(false), Times.Once);
        documents.Verify(d => d.SaveDocument(active.Path), Times.Once);
    }

    [Fact]
    public void DiagnoseActiveDocumentHealth_ProjectsCorrelatedDiagnosticsIntoActionableSummary()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var selection = new Mock<ISelectionService>();
        var logger = new RecordingWorkflowStageLogger();
        var active = new SwDocumentInfo(@"C:\Asm.sldasm", "Asm", 2);
        var persistentAssemblyIssue = CorrelatedIssue(
            "MateCoincident1",
            "MateCoincident",
            46,
            isWarning: false,
            DiagnosticTargetContext(
                scopeType: "assembly_level",
                documentPath: active.Path,
                reason: "mate_feature"));
        var resolvedSharedScopeIssue = CorrelatedIssue(
            "CutListFolder1",
            "CutListFolder",
            99,
            isWarning: false,
            DiagnosticTargetContext(
                scopeType: "shared_source_scope",
                componentName: "Plate-1",
                sourceFilePath: @"C:\Plate.sldprt",
                documentPath: active.Path,
                isExact: false,
                isAmbiguous: true,
                sourceFileReuseCount: 2,
                reason: "shared source ambiguity",
                matchingInstances:
                [
                    new ComponentInstanceInfo("Plate-1", @"C:\Plate.sldprt", "SubAsm-1/Plate-1", 1),
                    new ComponentInstanceInfo("Plate-2", @"C:\Plate.sldprt", "SubAsm-2/Plate-2", 1),
                ]));
        var introducedComponentWarning = CorrelatedIssue(
            "Boss-Extrude2",
            "Boss",
            12,
            isWarning: true,
            DiagnosticTargetContext(
                scopeType: "component_instance",
                hierarchyPath: "SubAsm-1/Bracket-1",
                componentName: "Bracket-1",
                sourceFilePath: @"C:\Bracket.sldprt",
                documentPath: active.Path,
                owningAssemblyHierarchyPath: "SubAsm-1",
                owningAssemblyFilePath: @"C:\SubAsm.sldasm"));

        documents.Setup(d => d.GetActiveDocument()).Returns(active);
        selection.Setup(s => s.GetEditState()).Returns(new EditStateInfo(false, "None", true, true));
        selection.SetupSequence(s => s.GetFeatureDiagnostics())
            .Returns(FeatureDiagnosticsWithCorrelatedIssues(persistentAssemblyIssue, resolvedSharedScopeIssue))
            .Returns(FeatureDiagnosticsWithCorrelatedIssues(persistentAssemblyIssue, introducedComponentWarning));
        documents.Setup(d => d.ForceRebuildActiveDocument(false)).Returns(new RebuildExecutionResult(true, true, false, RebuildState(1), RebuildState(0)));

        var service = new WorkflowService(documents.Object, assembly.Object, selection.Object, null, logger);

        var result = service.DiagnoseActiveDocumentHealth(forceRebuild: true, topOnly: false, saveDocument: false);

        Assert.Equal("completed", result.Status);
        Assert.True(result.HasBlockingIssues);
        Assert.True(result.HasWarnings);
        Assert.NotNull(result.ActionableDiagnostics);
        Assert.Equal(2, result.ActionableDiagnostics!.CurrentIssues.Count);
        Assert.Single(result.ActionableDiagnostics.BlockingIssues);
        Assert.Single(result.ActionableDiagnostics.WarningIssues);
        Assert.Single(result.ActionableDiagnostics.ResolvedByRebuildIssues);
        Assert.Single(result.ActionableDiagnostics.IntroducedByRebuildIssues);
        Assert.Equal("assembly_level", result.ActionableDiagnostics.CurrentIssues[0].TargetContext.ScopeType);
        Assert.Equal("component_instance", result.ActionableDiagnostics.CurrentIssues[1].TargetContext.ScopeType);
        Assert.Equal("SubAsm-1/Bracket-1", result.ActionableDiagnostics.WarningIssues[0].TargetContext.HierarchyPath);
        Assert.Equal("shared_source_scope", result.ActionableDiagnostics.ResolvedByRebuildIssues[0].TargetContext.ScopeType);
        AssertStageLogged(logger, nameof(WorkflowService.DiagnoseActiveDocumentHealth), "verification.feature_diagnostics_before_rebuild", "completed", "correlatedIssues=2");
        AssertStageLogged(logger, nameof(WorkflowService.DiagnoseActiveDocumentHealth), "verification.feature_diagnostics_after_rebuild", "completed", "correlatedIssues=2");
        AssertStageLogged(logger, nameof(WorkflowService.DiagnoseActiveDocumentHealth), "final", "completed", "actionableIssues=2");
    }

    [Fact]
    public void DiagnoseActiveDocumentHealth_OnPlannedNextVersion_AttachesCompatibilityAdvisory()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var selection = new Mock<ISelectionService>();
        var connectionManager = new Mock<ISwConnectionManager>();
        var active = new SwDocumentInfo(@"C:\Asm.sldasm", "Asm", 2);

        connectionManager.Setup(c => c.GetCompatibilityInfo())
            .Returns(Compatibility(
                "planned-next-version",
                "SolidWorks 2025 is within the planned certification window.",
                2025,
                "33.1.0",
                "swLicenseType_e.swLicenseStandard",
                "Runtime is newer than the compiled interop baseline but within the planned certification window."));
        documents.Setup(d => d.GetActiveDocument()).Returns(active);
        selection.Setup(s => s.GetEditState()).Returns(new EditStateInfo(false, "None", true, true));
        selection.SetupSequence(s => s.GetFeatureDiagnostics())
            .Returns(FeatureDiagnostics())
            .Returns(FeatureDiagnostics());
        documents.Setup(d => d.GetActiveDocumentRebuildState()).Returns(RebuildState(0));

        var service = new WorkflowService(documents.Object, assembly.Object, selection.Object, connectionManager.Object);

        var result = service.DiagnoseActiveDocumentHealth(forceRebuild: false, saveDocument: false);

        Assert.Equal("completed", result.Status);
        Assert.NotNull(result.CompatibilityAdvisory);
        Assert.Equal("planned-next-version", result.CompatibilityAdvisory!.CompatibilityState);
        Assert.Equal("info", result.CompatibilityAdvisory.AdvisoryLevel);
        Assert.Equal("33.1.0", result.CompatibilityAdvisory.RuntimeRevisionNumber);
        Assert.Equal("swLicenseType_e.swLicenseStandard", result.CompatibilityAdvisory.LicenseName);
    }

    [Fact]
    public void DiagnoseActiveDocumentHealth_WhenCompatibilityProbeFails_StillReturnsWorkflowResult()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var selection = new Mock<ISelectionService>();
        var connectionManager = new Mock<ISwConnectionManager>();
        var active = new SwDocumentInfo(@"C:\Asm.sldasm", "Asm", 2);

        connectionManager.Setup(c => c.GetCompatibilityInfo()).Throws(new InvalidOperationException("probe failed"));
        documents.Setup(d => d.GetActiveDocument()).Returns(active);
        selection.Setup(s => s.GetEditState()).Returns(new EditStateInfo(false, "None", true, true));
        selection.SetupSequence(s => s.GetFeatureDiagnostics())
            .Returns(FeatureDiagnostics())
            .Returns(FeatureDiagnostics(warnings: 1));
        documents.Setup(d => d.GetActiveDocumentRebuildState()).Returns(RebuildState(0));

        var service = new WorkflowService(documents.Object, assembly.Object, selection.Object, connectionManager.Object);

        var result = service.DiagnoseActiveDocumentHealth(forceRebuild: false, saveDocument: false);

        Assert.Equal("completed", result.Status);
        Assert.True(result.HasWarnings);
        Assert.Null(result.CompatibilityAdvisory);
    }

    [Fact]
    public void DiagnoseActiveDocumentHealth_WhenSaveFails_ReturnsSaveFailedStatus()
    {
        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var selection = new Mock<ISelectionService>();
        var active = new SwDocumentInfo(@"C:\Asm.sldasm", "Asm", 2);
        documents.Setup(d => d.GetActiveDocument()).Returns(active);
        selection.Setup(s => s.GetEditState()).Returns(new EditStateInfo(false, "None", true, true));
        selection.SetupSequence(s => s.GetFeatureDiagnostics())
            .Returns(FeatureDiagnostics())
            .Returns(FeatureDiagnostics());
        documents.Setup(d => d.GetActiveDocumentRebuildState()).Returns(RebuildState(0));
        documents.Setup(d => d.SaveDocument(active.Path)).Throws(new SolidWorksApiException(
            "IModelDoc2.Save3",
            "save failed",
            diagnostics: Diagnostics()));

        var service = new WorkflowService(documents.Object, assembly.Object, selection.Object);

        var result = service.DiagnoseActiveDocumentHealth(forceRebuild: false, saveDocument: true);

        Assert.Equal("save_failed", result.Status);
        Assert.True(result.HasBlockingIssues);
        Assert.False(result.ReadyForVerificationGate);
        Assert.True(result.SaveHealth.SaveAttempted);
        Assert.False(result.SaveHealth.SaveSucceeded);
    }

    [Fact]
    public void ReplaceNestedComponentAndVerifyPersistence_WhenTargetIsTopLevel_ReturnsFailureWithoutMutation()
    {
        var replacementFilePath = TempFilePath("BracketNew.sldprt");
        File.WriteAllText(replacementFilePath, "placeholder");

        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var logger = new RecordingWorkflowStageLogger();
        documents.Setup(d => d.GetActiveDocument()).Returns(new SwDocumentInfo(@"C:\Top.sldasm", "Top", 2));

        var resolution = ResolvedTarget(hierarchyPath: "Bracket-1", sourcePath: @"C:\Bracket.sldprt", owningHierarchyPath: null!, owningAssemblyFilePath: null!, depth: 0, reuseCount: 1) with
        {
            OwningAssemblyHierarchyPath = null,
            OwningAssemblyFilePath = null,
        };
        assembly.Setup(a => a.ResolveComponentTarget(null, "Bracket-1", null)).Returns(resolution);
        assembly.Setup(a => a.AnalyzeSharedPartEditImpact(null, "Bracket-1", null)).Returns(Impact(resolution, @"C:\Bracket.sldprt", 1, true, "Bracket-1"));

        var service = new WorkflowService(documents.Object, assembly.Object, null, null, logger);

        var result = service.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, hierarchyPath: "Bracket-1");

        Assert.Equal("target_not_nested", result.Status);
        Assert.False(result.PersistenceVerified);
        AssertStageLogged(logger, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "preconditions.replacement_file", "completed", replacementFilePath);
        AssertStageLogged(logger, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "preconditions.target_validation", "failed", "target_not_nested");
        AssertStageLogged(logger, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "final", "failed", "status=target_not_nested");
        documents.Verify(d => d.OpenDocument(It.IsAny<string>()), Times.Never);
        assembly.Verify(a => a.ReplaceComponent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void ReplaceNestedComponentAndVerifyPersistence_CompletesAndVerifiesPersistence()
    {
        var replacementFilePath = TempFilePath("ReplacementPulley.sldprt");
        File.WriteAllText(replacementFilePath, "placeholder");

        var documents = new Mock<IDocumentService>();
        var assembly = new Mock<IAssemblyService>();
        var logger = new RecordingWorkflowStageLogger();
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

        var service = new WorkflowService(documents.Object, assembly.Object, null, null, logger);

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
        AssertStageLogged(logger, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "mutation.replace_component", "completed", "Pulley-1");
        AssertStageLogged(logger, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "verification.persistence_resolution", "completed", "resolved=true");
        AssertStageLogged(logger, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "verification.post_replacement_shared_part_impact", "completed", "affectedInstanceCount=1");
        AssertStageLogged(logger, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "final", "completed", "status=completed");
        documents.Verify(d => d.CloseDocument(@"C:\Top.sldasm"), Times.Once);
        documents.Verify(d => d.CloseDocument(@"C:\SubAsm.sldasm"), Times.Once);
        assembly.Verify(a => a.ReplaceComponent("Pulley-1", replacementFilePath, "", false, 0, true), Times.Once);
    }
}
