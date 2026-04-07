using SolidWorks.Interop.swconst;

namespace SolidWorksBridge.SolidWorks;

public record CompatibilityAdvisory(
    string CompatibilityState,
    string AdvisoryLevel,
    string Summary,
    string RuntimeRevisionNumber,
    int? RuntimeMarketingYear,
    string LicenseName,
    IReadOnlyList<string> Notices);

public record NestedComponentReplacementWorkflowResult(
    AssemblyTargetResolutionResult InitialTargetResolution,
    SharedPartEditImpactResult PreReplacementImpactAnalysis,
    string? ParentAssemblyFilePath,
    string? OwningAssemblyHierarchyPath,
    string? OwningAssemblyFilePath,
    string? ReplacementTargetHierarchyPath,
    string ReplacementFilePath,
    bool OwningAssemblyActivated,
    AssemblyComponentReplacementResult? ReplacementResult,
    SwSaveResult? SaveResult,
    bool ParentAssemblyReloaded,
    AssemblyTargetResolutionResult? PersistenceResolution,
    SharedPartEditImpactResult? PostReplacementImpactAnalysis,
    bool PersistenceVerified,
    string Status,
    string? FailureReason,
    CompatibilityAdvisory? CompatibilityAdvisory = null);

public record TargetedStaticInterferenceReviewResult(
    IReadOnlyList<string> RequestedHierarchyPaths,
    bool TreatCoincidenceAsInterference,
    AssemblyTargetResolutionResult FirstTargetResolution,
    AssemblyTargetResolutionResult SecondTargetResolution,
    IReadOnlyList<string> CheckedHierarchyPaths,
    AssemblyInterferenceCheckResult? InterferenceCheck,
    bool ScopeValidated,
    bool ScopeEvaluatedAsRequested,
    bool HasInterference,
    string Status,
    string? FailureReason,
    CompatibilityAdvisory? CompatibilityAdvisory = null);

public record SaveHealthInfo(
    bool SaveAttempted,
    bool SaveSucceeded,
    string? DocumentPath,
    SwSaveResult? SaveResult,
    SwApiDiagnostics? Diagnostics,
    bool HasErrors,
    bool HasWarnings,
    string? FailureReason);

public record ActiveDocumentHealthDiagnosticsResult(
    SwDocumentInfo? ActiveDocument,
    EditStateInfo? EditState,
    FeatureDiagnosticsResult? FeatureDiagnosticsBeforeRebuild,
    RebuildExecutionResult Rebuild,
    FeatureDiagnosticsResult? FeatureDiagnosticsAfterRebuild,
    SaveHealthInfo SaveHealth,
    bool HasBlockingIssues,
    bool HasWarnings,
    bool ReadyForVerificationGate,
    string Status,
    string? FailureReason,
    CompatibilityAdvisory? CompatibilityAdvisory = null);

public interface IWorkflowService
{
    ActiveDocumentHealthDiagnosticsResult DiagnoseActiveDocumentHealth(
        bool forceRebuild = true,
        bool topOnly = false,
        bool saveDocument = false);

    TargetedStaticInterferenceReviewResult ReviewTargetedStaticInterference(
        string firstHierarchyPath,
        string secondHierarchyPath,
        bool treatCoincidenceAsInterference = false);

    NestedComponentReplacementWorkflowResult ReplaceNestedComponentAndVerifyPersistence(
        string replacementFilePath,
        string? componentName = null,
        string? hierarchyPath = null,
        string? componentPath = null,
        string configName = "",
        int useConfigChoice = (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_MatchName,
        bool reattachMates = true);
}

public class WorkflowService : IWorkflowService
{
    private readonly IDocumentService _documents;
    private readonly IAssemblyService _assembly;
    private readonly ISelectionService? _selection;
    private readonly ISwConnectionManager? _connectionManager;

    public WorkflowService(IDocumentService documents, IAssemblyService assembly)
        : this(documents, assembly, null, null)
    {
    }

    public WorkflowService(IDocumentService documents, IAssemblyService assembly, ISelectionService? selection)
        : this(documents, assembly, selection, null)
    {
    }

    public WorkflowService(
        IDocumentService documents,
        IAssemblyService assembly,
        ISelectionService? selection,
        ISwConnectionManager? connectionManager)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _selection = selection;
        _connectionManager = connectionManager;
    }

    public ActiveDocumentHealthDiagnosticsResult DiagnoseActiveDocumentHealth(
        bool forceRebuild = true,
        bool topOnly = false,
        bool saveDocument = false)
    {
        var compatibilityAdvisory = TryGetCompatibilityAdvisory();

        if (_selection == null)
        {
            throw new InvalidOperationException("Active document health diagnostics require an ISelectionService instance.");
        }

        var activeDocument = _documents.GetActiveDocument();
        if (activeDocument == null)
        {
            return CreateHealthFailureResult(
                activeDocument: null,
                editState: null,
                featureDiagnosticsBeforeRebuild: null,
                rebuild: CreateNoOpRebuildResult(topOnly),
                featureDiagnosticsAfterRebuild: null,
                saveHealth: new SaveHealthInfo(false, false, null, null, null, false, false, null),
                status: "no_active_document",
                failureReason: "No active document.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        var editState = _selection.GetEditState();
        if (editState.IsEditing)
        {
            return CreateHealthFailureResult(
                activeDocument,
                editState,
                featureDiagnosticsBeforeRebuild: null,
                rebuild: CreateNoOpRebuildResult(topOnly),
                featureDiagnosticsAfterRebuild: null,
                saveHealth: new SaveHealthInfo(false, false, activeDocument.Path, null, null, false, false, null),
                status: "editing_state_blocks_diagnostics",
                failureReason: "Finish the active sketch or edit mode before running document health diagnostics.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        var featureDiagnosticsBeforeRebuild = _selection.GetFeatureDiagnostics();
        RebuildExecutionResult rebuild;
        if (forceRebuild)
        {
            rebuild = _documents.ForceRebuildActiveDocument(topOnly);
        }
        else
        {
            var currentState = _documents.GetActiveDocumentRebuildState();
            rebuild = new RebuildExecutionResult(
                RebuildAttempted: false,
                RebuildSucceeded: true,
                TopOnly: topOnly,
                StatusBefore: currentState,
                StatusAfter: currentState);
        }

        var featureDiagnosticsAfterRebuild = _selection.GetFeatureDiagnostics();
        var saveHealth = EvaluateSaveHealth(activeDocument, saveDocument);

        bool hasBlockingIssues = rebuild.StatusAfter.NeedsRebuild
            || featureDiagnosticsAfterRebuild.ErrorCount > 0
            || saveHealth.HasErrors
            || (saveHealth.SaveAttempted && !saveHealth.SaveSucceeded);
        bool hasWarnings = featureDiagnosticsAfterRebuild.WarningCount > 0 || saveHealth.HasWarnings;
        bool readyForVerificationGate = !hasBlockingIssues;
        string status = saveHealth.SaveAttempted && !saveHealth.SaveSucceeded
            ? "save_failed"
            : "completed";
        string? failureReason = saveHealth.SaveAttempted && !saveHealth.SaveSucceeded
            ? saveHealth.FailureReason
            : null;

        return new ActiveDocumentHealthDiagnosticsResult(
            ActiveDocument: activeDocument,
            EditState: editState,
            FeatureDiagnosticsBeforeRebuild: featureDiagnosticsBeforeRebuild,
            Rebuild: rebuild,
            FeatureDiagnosticsAfterRebuild: featureDiagnosticsAfterRebuild,
            SaveHealth: saveHealth,
            HasBlockingIssues: hasBlockingIssues,
            HasWarnings: hasWarnings,
            ReadyForVerificationGate: readyForVerificationGate,
            Status: status,
            FailureReason: failureReason,
            CompatibilityAdvisory: compatibilityAdvisory);
    }

    public TargetedStaticInterferenceReviewResult ReviewTargetedStaticInterference(
        string firstHierarchyPath,
        string secondHierarchyPath,
        bool treatCoincidenceAsInterference = false)
    {
        if (string.IsNullOrWhiteSpace(firstHierarchyPath))
        {
            throw new ArgumentException("firstHierarchyPath must not be empty", nameof(firstHierarchyPath));
        }

        if (string.IsNullOrWhiteSpace(secondHierarchyPath))
        {
            throw new ArgumentException("secondHierarchyPath must not be empty", nameof(secondHierarchyPath));
        }

        string normalizedFirstHierarchyPath = firstHierarchyPath.Trim();
        string normalizedSecondHierarchyPath = secondHierarchyPath.Trim();
        var compatibilityAdvisory = TryGetCompatibilityAdvisory();
        var firstResolution = _assembly.ResolveComponentTarget(hierarchyPath: normalizedFirstHierarchyPath);
        var secondResolution = _assembly.ResolveComponentTarget(hierarchyPath: normalizedSecondHierarchyPath);

        if (!firstResolution.IsResolved || firstResolution.ResolvedInstance == null)
        {
            return CreateTargetedInterferenceFailureResult(
                normalizedFirstHierarchyPath,
                normalizedSecondHierarchyPath,
                treatCoincidenceAsInterference,
                firstResolution,
                secondResolution,
                status: firstResolution.IsAmbiguous ? "first_target_ambiguous" : "first_target_not_resolved",
                failureReason: firstResolution.IsAmbiguous
                    ? "The first requested hierarchy path resolved to multiple component instances."
                    : "The first requested hierarchy path does not resolve to a component in the active assembly.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        if (!secondResolution.IsResolved || secondResolution.ResolvedInstance == null)
        {
            return CreateTargetedInterferenceFailureResult(
                normalizedFirstHierarchyPath,
                normalizedSecondHierarchyPath,
                treatCoincidenceAsInterference,
                firstResolution,
                secondResolution,
                status: secondResolution.IsAmbiguous ? "second_target_ambiguous" : "second_target_not_resolved",
                failureReason: secondResolution.IsAmbiguous
                    ? "The second requested hierarchy path resolved to multiple component instances."
                    : "The second requested hierarchy path does not resolve to a component in the active assembly.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        if (string.Equals(firstResolution.ResolvedInstance.HierarchyPath, secondResolution.ResolvedInstance.HierarchyPath, StringComparison.OrdinalIgnoreCase))
        {
            return CreateTargetedInterferenceFailureResult(
                normalizedFirstHierarchyPath,
                normalizedSecondHierarchyPath,
                treatCoincidenceAsInterference,
                firstResolution,
                secondResolution,
                status: "targets_not_distinct",
                failureReason: "Targeted static interference review requires two distinct component instances.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        var checkedHierarchyPaths = new[]
        {
            firstResolution.ResolvedInstance.HierarchyPath,
            secondResolution.ResolvedInstance.HierarchyPath,
        };

        var interferenceCheck = _assembly.CheckInterference(checkedHierarchyPaths, treatCoincidenceAsInterference);
        bool scopeEvaluatedAsRequested = interferenceCheck.CheckedComponentCount == checkedHierarchyPaths.Length;

        if (!scopeEvaluatedAsRequested)
        {
            return new TargetedStaticInterferenceReviewResult(
                RequestedHierarchyPaths: new[] { normalizedFirstHierarchyPath, normalizedSecondHierarchyPath },
                TreatCoincidenceAsInterference: treatCoincidenceAsInterference,
                FirstTargetResolution: firstResolution,
                SecondTargetResolution: secondResolution,
                CheckedHierarchyPaths: checkedHierarchyPaths,
                InterferenceCheck: interferenceCheck,
                ScopeValidated: true,
                ScopeEvaluatedAsRequested: false,
                HasInterference: false,
                Status: "scope_not_evaluated_as_requested",
                FailureReason: $"The interference check reported {interferenceCheck.CheckedComponentCount} checked component(s), expected {checkedHierarchyPaths.Length}.",
                CompatibilityAdvisory: compatibilityAdvisory);
        }

        return new TargetedStaticInterferenceReviewResult(
            RequestedHierarchyPaths: new[] { normalizedFirstHierarchyPath, normalizedSecondHierarchyPath },
            TreatCoincidenceAsInterference: treatCoincidenceAsInterference,
            FirstTargetResolution: firstResolution,
            SecondTargetResolution: secondResolution,
            CheckedHierarchyPaths: checkedHierarchyPaths,
            InterferenceCheck: interferenceCheck,
            ScopeValidated: true,
            ScopeEvaluatedAsRequested: true,
            HasInterference: interferenceCheck.HasInterference,
            Status: "completed",
            FailureReason: null,
            CompatibilityAdvisory: compatibilityAdvisory);
    }

    public NestedComponentReplacementWorkflowResult ReplaceNestedComponentAndVerifyPersistence(
        string replacementFilePath,
        string? componentName = null,
        string? hierarchyPath = null,
        string? componentPath = null,
        string configName = "",
        int useConfigChoice = (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_MatchName,
        bool reattachMates = true)
    {
        if (string.IsNullOrWhiteSpace(replacementFilePath))
        {
            throw new ArgumentException("replacementFilePath must not be empty", nameof(replacementFilePath));
        }

        string normalizedReplacementFilePath = Path.GetFullPath(replacementFilePath);
        if (!File.Exists(normalizedReplacementFilePath))
        {
            throw new FileNotFoundException($"Replacement component file was not found: {normalizedReplacementFilePath}", normalizedReplacementFilePath);
        }

        var compatibilityAdvisory = TryGetCompatibilityAdvisory();

        var activeDocument = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        string? parentAssemblyFilePath = NormalizePathOrNull(activeDocument.Path);

        var initialResolution = _assembly.ResolveComponentTarget(componentName, hierarchyPath, componentPath);
        var preReplacementImpact = _assembly.AnalyzeSharedPartEditImpact(componentName, hierarchyPath, componentPath);

        if (!initialResolution.IsResolved || initialResolution.ResolvedInstance == null)
        {
            return CreateFailureResult(
                initialResolution,
                preReplacementImpact,
                parentAssemblyFilePath,
                normalizedReplacementFilePath,
                status: initialResolution.IsAmbiguous ? "target_ambiguous" : "target_not_resolved",
                failureReason: initialResolution.IsAmbiguous
                    ? "The requested target matched multiple component instances."
                    : "The requested target could not be resolved to a single component instance.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        if (initialResolution.ResolvedInstance.Depth == 0 || string.IsNullOrWhiteSpace(initialResolution.OwningAssemblyHierarchyPath))
        {
            return CreateFailureResult(
                initialResolution,
                preReplacementImpact,
                parentAssemblyFilePath,
                normalizedReplacementFilePath,
                status: "target_not_nested",
                failureReason: "The resolved component is already top-level in the active assembly. Use the direct replace workflow instead.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        if (string.IsNullOrWhiteSpace(parentAssemblyFilePath))
        {
            return CreateFailureResult(
                initialResolution,
                preReplacementImpact,
                parentAssemblyFilePath,
                normalizedReplacementFilePath,
                status: "parent_assembly_not_saved",
                failureReason: "The active parent assembly must be saved before persistence can be verified by reopening it.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        string? owningAssemblyFilePath = NormalizePathOrNull(initialResolution.OwningAssemblyFilePath);
        if (string.IsNullOrWhiteSpace(owningAssemblyFilePath))
        {
            return CreateFailureResult(
                initialResolution,
                preReplacementImpact,
                parentAssemblyFilePath,
                normalizedReplacementFilePath,
                status: "owning_assembly_not_saved",
                failureReason: "The owning subassembly must be saved before nested replacement can be verified.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        string? sourceFilePath = NormalizePathOrNull(initialResolution.ResolvedInstance.Path);
        if (!string.IsNullOrWhiteSpace(sourceFilePath) && PathsEqual(sourceFilePath, normalizedReplacementFilePath))
        {
            return CreateFailureResult(
                initialResolution,
                preReplacementImpact,
                parentAssemblyFilePath,
                normalizedReplacementFilePath,
                status: "replacement_matches_source_file",
                failureReason: "The replacement file matches the currently resolved component source file, so the workflow would be a no-op.",
                compatibilityAdvisory: compatibilityAdvisory);
        }

        string replacementTargetHierarchyPath = GetReplacementTargetHierarchyPath(
            initialResolution.ResolvedInstance.HierarchyPath,
            initialResolution.OwningAssemblyHierarchyPath!);

        if (replacementTargetHierarchyPath.Contains('/'))
        {
            return CreateFailureResult(
                initialResolution,
                preReplacementImpact,
                parentAssemblyFilePath,
                normalizedReplacementFilePath,
                status: "target_not_top_level_in_owning_context",
                failureReason: "The resolved target is not a direct child of the owning assembly context.",
                owningAssemblyHierarchyPath: initialResolution.OwningAssemblyHierarchyPath,
                owningAssemblyFilePath: owningAssemblyFilePath,
                replacementTargetHierarchyPath: replacementTargetHierarchyPath,
                compatibilityAdvisory: compatibilityAdvisory);
        }

        _documents.OpenDocument(owningAssemblyFilePath);
        bool owningAssemblyActivated = IsActiveDocument(owningAssemblyFilePath);
        if (!owningAssemblyActivated)
        {
            return CreateFailureResult(
                initialResolution,
                preReplacementImpact,
                parentAssemblyFilePath,
                normalizedReplacementFilePath,
                status: "owning_assembly_not_active",
                failureReason: "The owning subassembly did not become the active document before replacement.",
                owningAssemblyHierarchyPath: initialResolution.OwningAssemblyHierarchyPath,
                owningAssemblyFilePath: owningAssemblyFilePath,
                replacementTargetHierarchyPath: replacementTargetHierarchyPath,
                owningAssemblyActivated: false,
                compatibilityAdvisory: compatibilityAdvisory);
        }

        var replacementResult = _assembly.ReplaceComponent(
            replacementTargetHierarchyPath,
            normalizedReplacementFilePath,
            configName,
            replaceAllInstances: false,
            useConfigChoice,
            reattachMates);

        var saveResult = _documents.SaveDocument(owningAssemblyFilePath);

        TryCloseDocument(parentAssemblyFilePath);
        TryCloseDocument(owningAssemblyFilePath);

        _documents.OpenDocument(parentAssemblyFilePath);
        bool parentAssemblyReloaded = IsActiveDocument(parentAssemblyFilePath);
        if (!parentAssemblyReloaded)
        {
            return CreateFailureResult(
                initialResolution,
                preReplacementImpact,
                parentAssemblyFilePath,
                normalizedReplacementFilePath,
                status: "parent_assembly_not_reloaded",
                failureReason: "The parent assembly could not be reactivated after the owning subassembly was saved.",
                owningAssemblyHierarchyPath: initialResolution.OwningAssemblyHierarchyPath,
                owningAssemblyFilePath: owningAssemblyFilePath,
                replacementTargetHierarchyPath: replacementTargetHierarchyPath,
                owningAssemblyActivated: true,
                replacementResult: replacementResult,
                saveResult: saveResult,
                parentAssemblyReloaded: false,
                compatibilityAdvisory: compatibilityAdvisory);
        }

        var persistenceResolution = ResolvePersistedTarget(initialResolution, normalizedReplacementFilePath);
        SharedPartEditImpactResult? postReplacementImpact = persistenceResolution.IsResolved
            ? _assembly.AnalyzeSharedPartEditImpact(hierarchyPath: persistenceResolution.ResolvedInstance!.HierarchyPath)
            : null;

        bool persistenceVerified = persistenceResolution.IsResolved
            && persistenceResolution.ResolvedInstance != null
            && PathsEqual(persistenceResolution.ResolvedInstance.Path, normalizedReplacementFilePath);

        return new NestedComponentReplacementWorkflowResult(
            initialResolution,
            preReplacementImpact,
            parentAssemblyFilePath,
            initialResolution.OwningAssemblyHierarchyPath,
            owningAssemblyFilePath,
            replacementTargetHierarchyPath,
            normalizedReplacementFilePath,
            true,
            replacementResult,
            saveResult,
            true,
            persistenceResolution,
            postReplacementImpact,
            persistenceVerified,
            persistenceVerified ? "completed" : "persistence_verification_failed",
            persistenceVerified ? null : "The parent assembly reopened successfully, but the target still did not resolve to the replacement file.",
            compatibilityAdvisory);
    }

    private bool IsActiveDocument(string expectedPath)
    {
        var activeDocument = _documents.GetActiveDocument();
        return activeDocument != null && PathsEqual(activeDocument.Path, expectedPath);
    }

    private static TargetedStaticInterferenceReviewResult CreateTargetedInterferenceFailureResult(
        string firstHierarchyPath,
        string secondHierarchyPath,
        bool treatCoincidenceAsInterference,
        AssemblyTargetResolutionResult firstTargetResolution,
        AssemblyTargetResolutionResult secondTargetResolution,
        string status,
        string failureReason,
        CompatibilityAdvisory? compatibilityAdvisory)
    {
        return new TargetedStaticInterferenceReviewResult(
            RequestedHierarchyPaths: new[] { firstHierarchyPath, secondHierarchyPath },
            TreatCoincidenceAsInterference: treatCoincidenceAsInterference,
            FirstTargetResolution: firstTargetResolution,
            SecondTargetResolution: secondTargetResolution,
            CheckedHierarchyPaths: Array.Empty<string>(),
            InterferenceCheck: null,
            ScopeValidated: false,
            ScopeEvaluatedAsRequested: false,
            HasInterference: false,
            Status: status,
            FailureReason: failureReason,
            CompatibilityAdvisory: compatibilityAdvisory);
    }

    private AssemblyTargetResolutionResult ResolvePersistedTarget(
        AssemblyTargetResolutionResult initialResolution,
        string replacementFilePath)
    {
        var resolvedInstance = initialResolution.ResolvedInstance
            ?? throw new InvalidOperationException("Initial target resolution must contain a resolved instance.");

        var recursiveComponents = _assembly.ListComponentsRecursive();
        var exactHierarchyMatch = recursiveComponents
            .Where(component => string.Equals(component.HierarchyPath, resolvedInstance.HierarchyPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ComponentInstanceInfo? persistedInstance = exactHierarchyMatch.Count == 1 && PathsEqual(exactHierarchyMatch[0].Path, replacementFilePath)
            ? exactHierarchyMatch[0]
            : null;

        var owningContextMatches = recursiveComponents
            .Where(component =>
                string.Equals(GetParentHierarchyPath(component.HierarchyPath), initialResolution.OwningAssemblyHierarchyPath, StringComparison.OrdinalIgnoreCase)
                && PathsEqual(component.Path, replacementFilePath))
            .ToList();

        if (persistedInstance == null && owningContextMatches.Count == 1)
        {
            persistedInstance = owningContextMatches[0];
        }

        IReadOnlyList<ComponentInstanceInfo> matchingInstances = persistedInstance != null
            ? new[] { persistedInstance }
            : owningContextMatches;
        int sourceFileReuseCount = recursiveComponents.Count(component => PathsEqual(component.Path, replacementFilePath));

        return new AssemblyTargetResolutionResult(
            RequestedName: resolvedInstance.Name,
            RequestedHierarchyPath: resolvedInstance.HierarchyPath,
            RequestedComponentPath: replacementFilePath,
            IsResolved: persistedInstance != null,
            IsAmbiguous: persistedInstance == null && matchingInstances.Count > 1,
            ResolvedInstance: persistedInstance,
            OwningAssemblyHierarchyPath: initialResolution.OwningAssemblyHierarchyPath,
            OwningAssemblyFilePath: initialResolution.OwningAssemblyFilePath,
            SourceFileReuseCount: sourceFileReuseCount,
            MatchingInstances: matchingInstances);
    }

    private void TryCloseDocument(string path)
    {
        try
        {
            _documents.CloseDocument(path);
        }
        catch
        {
            // Best effort: OpenDocument can still reactivate an already-open file.
        }
    }

    private static NestedComponentReplacementWorkflowResult CreateFailureResult(
        AssemblyTargetResolutionResult initialResolution,
        SharedPartEditImpactResult preReplacementImpact,
        string? parentAssemblyFilePath,
        string replacementFilePath,
        string status,
        string failureReason,
        string? owningAssemblyHierarchyPath = null,
        string? owningAssemblyFilePath = null,
        string? replacementTargetHierarchyPath = null,
        bool owningAssemblyActivated = false,
        AssemblyComponentReplacementResult? replacementResult = null,
        SwSaveResult? saveResult = null,
        bool parentAssemblyReloaded = false,
        AssemblyTargetResolutionResult? persistenceResolution = null,
        SharedPartEditImpactResult? postReplacementImpactAnalysis = null,
        CompatibilityAdvisory? compatibilityAdvisory = null)
    {
        return new NestedComponentReplacementWorkflowResult(
            initialResolution,
            preReplacementImpact,
            parentAssemblyFilePath,
            owningAssemblyHierarchyPath ?? initialResolution.OwningAssemblyHierarchyPath,
            owningAssemblyFilePath ?? NormalizePathOrNull(initialResolution.OwningAssemblyFilePath),
            replacementTargetHierarchyPath,
            replacementFilePath,
            owningAssemblyActivated,
            replacementResult,
            saveResult,
            parentAssemblyReloaded,
            persistenceResolution,
            postReplacementImpactAnalysis,
            false,
            status,
            failureReason,
            compatibilityAdvisory);
    }

    private SaveHealthInfo EvaluateSaveHealth(SwDocumentInfo activeDocument, bool saveDocument)
    {
        if (!saveDocument)
        {
            return new SaveHealthInfo(false, false, activeDocument.Path, null, null, false, false, null);
        }

        if (string.IsNullOrWhiteSpace(activeDocument.Path))
        {
            return new SaveHealthInfo(
                SaveAttempted: true,
                SaveSucceeded: false,
                DocumentPath: null,
                SaveResult: null,
                Diagnostics: null,
                HasErrors: true,
                HasWarnings: false,
                FailureReason: "The active document must be saved to a file path before save-health diagnostics can run.");
        }

        try
        {
            var saveResult = _documents.SaveDocument(activeDocument.Path);
            return new SaveHealthInfo(
                SaveAttempted: true,
                SaveSucceeded: true,
                DocumentPath: activeDocument.Path,
                SaveResult: saveResult,
                Diagnostics: saveResult.Diagnostics,
                HasErrors: saveResult.Errors != 0,
                HasWarnings: saveResult.Warnings != 0,
                FailureReason: null);
        }
        catch (SolidWorksApiException ex)
        {
            return new SaveHealthInfo(
                SaveAttempted: true,
                SaveSucceeded: false,
                DocumentPath: activeDocument.Path,
                SaveResult: null,
                Diagnostics: ex.Diagnostics,
                HasErrors: true,
                HasWarnings: ex.Diagnostics?.Warnings.Count > 0,
                FailureReason: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new SaveHealthInfo(
                SaveAttempted: true,
                SaveSucceeded: false,
                DocumentPath: activeDocument.Path,
                SaveResult: null,
                Diagnostics: null,
                HasErrors: true,
                HasWarnings: false,
                FailureReason: ex.Message);
        }
    }

    private static ActiveDocumentHealthDiagnosticsResult CreateHealthFailureResult(
        SwDocumentInfo? activeDocument,
        EditStateInfo? editState,
        FeatureDiagnosticsResult? featureDiagnosticsBeforeRebuild,
        RebuildExecutionResult rebuild,
        FeatureDiagnosticsResult? featureDiagnosticsAfterRebuild,
        SaveHealthInfo saveHealth,
        string status,
        string failureReason,
        CompatibilityAdvisory? compatibilityAdvisory)
    {
        return new ActiveDocumentHealthDiagnosticsResult(
            ActiveDocument: activeDocument,
            EditState: editState,
            FeatureDiagnosticsBeforeRebuild: featureDiagnosticsBeforeRebuild,
            Rebuild: rebuild,
            FeatureDiagnosticsAfterRebuild: featureDiagnosticsAfterRebuild,
            SaveHealth: saveHealth,
            HasBlockingIssues: true,
            HasWarnings: false,
            ReadyForVerificationGate: false,
            Status: status,
            FailureReason: failureReason,
            CompatibilityAdvisory: compatibilityAdvisory);
    }

    private CompatibilityAdvisory? TryGetCompatibilityAdvisory()
    {
        if (_connectionManager == null)
        {
            return null;
        }

        try
        {
            var compatibility = _connectionManager.GetCompatibilityInfo();
            if (string.Equals(compatibility.CompatibilityState, "certified-baseline", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return CreateCompatibilityAdvisory(compatibility);
        }
        catch
        {
            return null;
        }
    }

    private static CompatibilityAdvisory CreateCompatibilityAdvisory(SolidWorksCompatibilityInfo compatibility)
    {
        string advisoryLevel = string.Equals(compatibility.CompatibilityState, "planned-next-version", StringComparison.OrdinalIgnoreCase)
            ? "info"
            : "warning";

        return new CompatibilityAdvisory(
            compatibility.CompatibilityState,
            advisoryLevel,
            compatibility.Summary,
            compatibility.RuntimeVersion.RevisionNumber,
            compatibility.RuntimeVersion.MarketingYear,
            compatibility.License.Name,
            compatibility.Notices);
    }

    private static RebuildExecutionResult CreateNoOpRebuildResult(bool topOnly)
    {
        var fullyRebuilt = new RebuildStateInfo(
            RawStatus: 0,
            NeedsRebuild: false,
            StatusCodes: [new SwCodeInfo(0, nameof(swModelRebuildStatus_e.swModelRebuildStatus_FullyRebuilt), "The model does not currently need rebuild.")],
            Summary: "The model does not currently need rebuild.");
        return new RebuildExecutionResult(
            RebuildAttempted: false,
            RebuildSucceeded: true,
            TopOnly: topOnly,
            StatusBefore: fullyRebuilt,
            StatusAfter: fullyRebuilt);
    }

    private static string GetReplacementTargetHierarchyPath(string resolvedHierarchyPath, string owningAssemblyHierarchyPath)
    {
        if (string.IsNullOrWhiteSpace(resolvedHierarchyPath))
        {
            throw new ArgumentException("resolvedHierarchyPath must not be empty", nameof(resolvedHierarchyPath));
        }

        if (string.IsNullOrWhiteSpace(owningAssemblyHierarchyPath))
        {
            throw new ArgumentException("owningAssemblyHierarchyPath must not be empty", nameof(owningAssemblyHierarchyPath));
        }

        string prefix = owningAssemblyHierarchyPath + "/";
        if (!resolvedHierarchyPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Resolved hierarchy path '{resolvedHierarchyPath}' is not inside owning assembly '{owningAssemblyHierarchyPath}'.");
        }

        return resolvedHierarchyPath[prefix.Length..];
    }

    private static string? GetParentHierarchyPath(string hierarchyPath)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPath))
        {
            return null;
        }

        int separatorIndex = hierarchyPath.LastIndexOf('/');
        return separatorIndex < 0 ? null : hierarchyPath[..separatorIndex];
    }

    private static string? NormalizePathOrNull(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetFullPath(path);
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }
}