using SolidWorks.Interop.swconst;

namespace SolidWorksBridge.SolidWorks;

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

public record DocumentHealthActionableDiagnosticsInfo(
    IReadOnlyList<CorrelatedDiagnosticIssueInfo> CurrentIssues,
    IReadOnlyList<CorrelatedDiagnosticIssueInfo> BlockingIssues,
    IReadOnlyList<CorrelatedDiagnosticIssueInfo> WarningIssues,
    IReadOnlyList<CorrelatedDiagnosticIssueInfo> ResolvedByRebuildIssues,
    IReadOnlyList<CorrelatedDiagnosticIssueInfo> IntroducedByRebuildIssues);

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
    CompatibilityAdvisory? CompatibilityAdvisory = null,
    DocumentHealthActionableDiagnosticsInfo? ActionableDiagnostics = null);

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
    private readonly IWorkflowStageLogger _workflowStageLogger;

    public WorkflowService(IDocumentService documents, IAssemblyService assembly)
        : this(documents, assembly, null, null, null)
    {
    }

    public WorkflowService(IDocumentService documents, IAssemblyService assembly, ISelectionService? selection)
        : this(documents, assembly, selection, null, null)
    {
    }

    public WorkflowService(
        IDocumentService documents,
        IAssemblyService assembly,
        ISelectionService? selection,
        ISwConnectionManager? connectionManager)
        : this(documents, assembly, selection, connectionManager, null)
    {
    }

    public WorkflowService(
        IDocumentService documents,
        IAssemblyService assembly,
        ISelectionService? selection,
        ISwConnectionManager? connectionManager,
        IWorkflowStageLogger? workflowStageLogger)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _selection = selection;
        _connectionManager = connectionManager;
        _workflowStageLogger = workflowStageLogger ?? NullWorkflowStageLogger.Instance;
    }

    public ActiveDocumentHealthDiagnosticsResult DiagnoseActiveDocumentHealth(
        bool forceRebuild = true,
        bool topOnly = false,
        bool saveDocument = false)
    {
        const string workflowName = nameof(DiagnoseActiveDocumentHealth);

        try
        {
            var compatibilityAdvisory = CompatibilityPolicy.TryGetAdvisory(_connectionManager);

            if (_selection == null)
            {
                LogStageStarted(workflowName, "preconditions.selection_service");
                LogStageFailed(workflowName, "preconditions.selection_service", "ISelectionService is required.");
                throw new InvalidOperationException("Active document health diagnostics require an ISelectionService instance.");
            }

            LogStageStarted(workflowName, "preconditions.active_document");
            var activeDocument = _documents.GetActiveDocument();
            if (activeDocument == null)
            {
                LogStageFailed(workflowName, "preconditions.active_document", "status=no_active_document");
                return FinalizeWorkflow(
                    workflowName,
                    CreateHealthFailureResult(
                        activeDocument: null,
                        editState: null,
                        featureDiagnosticsBeforeRebuild: null,
                        rebuild: CreateNoOpRebuildResult(topOnly),
                        featureDiagnosticsAfterRebuild: null,
                        saveHealth: new SaveHealthInfo(false, false, null, null, null, false, false, null),
                        status: "no_active_document",
                        failureReason: "No active document.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            LogStageCompleted(workflowName, "preconditions.active_document", ComposeDetail($"path={activeDocument.Path ?? activeDocument.Title}"));

            LogStageStarted(workflowName, "preconditions.edit_state");
            var editState = _selection.GetEditState();
            if (editState.IsEditing)
            {
                LogStageFailed(workflowName, "preconditions.edit_state", ComposeDetail($"mode={editState.EditMode}", "status=editing_state_blocks_diagnostics"));
                return FinalizeWorkflow(
                    workflowName,
                    CreateHealthFailureResult(
                        activeDocument,
                        editState,
                        featureDiagnosticsBeforeRebuild: null,
                        rebuild: CreateNoOpRebuildResult(topOnly),
                        featureDiagnosticsAfterRebuild: null,
                        saveHealth: new SaveHealthInfo(false, false, activeDocument.Path, null, null, false, false, null),
                        status: "editing_state_blocks_diagnostics",
                        failureReason: "Finish the active sketch or edit mode before running document health diagnostics.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            LogStageCompleted(workflowName, "preconditions.edit_state", ComposeDetail($"mode={editState.EditMode}"));

            var featureDiagnosticsBeforeRebuild = ExecuteStage(
                workflowName,
                "verification.feature_diagnostics_before_rebuild",
                () => _selection.GetFeatureDiagnostics(),
                diagnostics => ComposeDetail(
                    $"errors={diagnostics.ErrorCount}",
                    $"warnings={diagnostics.WarningCount}",
                    $"correlatedIssues={GetCorrelatedIssueCount(diagnostics)}"));

            RebuildExecutionResult rebuild;
            if (forceRebuild)
            {
                rebuild = ExecuteStage(
                    workflowName,
                    "mutation.rebuild",
                    () => _documents.ForceRebuildActiveDocument(topOnly),
                    rebuildResult => ComposeDetail($"attempted={ToToken(rebuildResult.RebuildAttempted)}", $"needsRebuildAfter={ToToken(rebuildResult.StatusAfter.NeedsRebuild)}"));
            }
            else
            {
                LogStageSkipped(workflowName, "mutation.rebuild", "forceRebuild=false");
                var currentState = ExecuteStage(
                    workflowName,
                    "verification.rebuild_state",
                    () => _documents.GetActiveDocumentRebuildState(),
                    state => ComposeDetail($"needsRebuild={ToToken(state.NeedsRebuild)}"));
                rebuild = new RebuildExecutionResult(
                    RebuildAttempted: false,
                    RebuildSucceeded: true,
                    TopOnly: topOnly,
                    StatusBefore: currentState,
                    StatusAfter: currentState);
            }

            var featureDiagnosticsAfterRebuild = ExecuteStage(
                workflowName,
                "verification.feature_diagnostics_after_rebuild",
                () => _selection.GetFeatureDiagnostics(),
                diagnostics => ComposeDetail(
                    $"errors={diagnostics.ErrorCount}",
                    $"warnings={diagnostics.WarningCount}",
                    $"correlatedIssues={GetCorrelatedIssueCount(diagnostics)}"));
            var actionableDiagnostics = CreateActionableDiagnostics(
                featureDiagnosticsBeforeRebuild,
                featureDiagnosticsAfterRebuild);

            SaveHealthInfo saveHealth;
            if (saveDocument)
            {
                LogStageStarted(workflowName, "verification.save_document");
                saveHealth = EvaluateSaveHealth(activeDocument, saveDocument);
                if (!saveHealth.SaveSucceeded)
                {
                    LogStageFailed(
                        workflowName,
                        "verification.save_document",
                        ComposeDetail($"attempted={ToToken(saveHealth.SaveAttempted)}", $"errors={ToToken(saveHealth.HasErrors)}", saveHealth.FailureReason));
                }
                else
                {
                    LogStageCompleted(
                        workflowName,
                        "verification.save_document",
                        ComposeDetail($"attempted={ToToken(saveHealth.SaveAttempted)}", $"warnings={ToToken(saveHealth.HasWarnings)}"));
                }
            }
            else
            {
                LogStageSkipped(workflowName, "verification.save_document", "saveDocument=false");
                saveHealth = EvaluateSaveHealth(activeDocument, saveDocument);
            }

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

            return FinalizeWorkflow(
                workflowName,
                new ActiveDocumentHealthDiagnosticsResult(
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
                    CompatibilityAdvisory: compatibilityAdvisory,
                    ActionableDiagnostics: actionableDiagnostics));
        }
        catch (Exception ex)
        {
            LogUnhandledWorkflowException(workflowName, ex);
            throw;
        }
    }

    public TargetedStaticInterferenceReviewResult ReviewTargetedStaticInterference(
        string firstHierarchyPath,
        string secondHierarchyPath,
        bool treatCoincidenceAsInterference = false)
    {
        const string workflowName = nameof(ReviewTargetedStaticInterference);

        if (string.IsNullOrWhiteSpace(firstHierarchyPath))
        {
            throw new ArgumentException("firstHierarchyPath must not be empty", nameof(firstHierarchyPath));
        }

        if (string.IsNullOrWhiteSpace(secondHierarchyPath))
        {
            throw new ArgumentException("secondHierarchyPath must not be empty", nameof(secondHierarchyPath));
        }

        try
        {
            string normalizedFirstHierarchyPath = firstHierarchyPath.Trim();
            string normalizedSecondHierarchyPath = secondHierarchyPath.Trim();
            var compatibilityAdvisory = CompatibilityPolicy.TryGetAdvisory(_connectionManager);

            LogStageStarted(workflowName, "preconditions.first_target_resolution");
            var firstResolution = _assembly.ResolveComponentTarget(hierarchyPath: normalizedFirstHierarchyPath);
            if (!firstResolution.IsResolved || firstResolution.ResolvedInstance == null)
            {
                LogStageFailed(
                    workflowName,
                    "preconditions.first_target_resolution",
                    ComposeDetail($"status={(firstResolution.IsAmbiguous ? "first_target_ambiguous" : "first_target_not_resolved")}"));
            }
            else
            {
                LogStageCompleted(
                    workflowName,
                    "preconditions.first_target_resolution",
                    ComposeDetail($"hierarchyPath={firstResolution.ResolvedInstance.HierarchyPath}"));
            }

            LogStageStarted(workflowName, "preconditions.second_target_resolution");
            var secondResolution = _assembly.ResolveComponentTarget(hierarchyPath: normalizedSecondHierarchyPath);
            if (!secondResolution.IsResolved || secondResolution.ResolvedInstance == null)
            {
                LogStageFailed(
                    workflowName,
                    "preconditions.second_target_resolution",
                    ComposeDetail($"status={(secondResolution.IsAmbiguous ? "second_target_ambiguous" : "second_target_not_resolved")}"));
            }

            if (secondResolution.IsResolved && secondResolution.ResolvedInstance != null)
            {
                LogStageCompleted(
                    workflowName,
                    "preconditions.second_target_resolution",
                    ComposeDetail($"hierarchyPath={secondResolution.ResolvedInstance.HierarchyPath}"));
            }

            if (!firstResolution.IsResolved || firstResolution.ResolvedInstance == null)
            {
                return FinalizeWorkflow(
                    workflowName,
                    CreateTargetedInterferenceFailureResult(
                        normalizedFirstHierarchyPath,
                        normalizedSecondHierarchyPath,
                        treatCoincidenceAsInterference,
                        firstResolution,
                        secondResolution,
                        status: firstResolution.IsAmbiguous ? "first_target_ambiguous" : "first_target_not_resolved",
                        failureReason: firstResolution.IsAmbiguous
                            ? "The first requested hierarchy path resolved to multiple component instances."
                            : "The first requested hierarchy path does not resolve to a component in the active assembly.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            if (!secondResolution.IsResolved || secondResolution.ResolvedInstance == null)
            {
                return FinalizeWorkflow(
                    workflowName,
                    CreateTargetedInterferenceFailureResult(
                        normalizedFirstHierarchyPath,
                        normalizedSecondHierarchyPath,
                        treatCoincidenceAsInterference,
                        firstResolution,
                        secondResolution,
                        status: secondResolution.IsAmbiguous ? "second_target_ambiguous" : "second_target_not_resolved",
                        failureReason: secondResolution.IsAmbiguous
                            ? "The second requested hierarchy path resolved to multiple component instances."
                            : "The second requested hierarchy path does not resolve to a component in the active assembly.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            LogStageStarted(workflowName, "preconditions.distinct_targets");
            if (string.Equals(firstResolution.ResolvedInstance.HierarchyPath, secondResolution.ResolvedInstance.HierarchyPath, StringComparison.OrdinalIgnoreCase))
            {
                LogStageFailed(workflowName, "preconditions.distinct_targets", "status=targets_not_distinct");
                return FinalizeWorkflow(
                    workflowName,
                    CreateTargetedInterferenceFailureResult(
                        normalizedFirstHierarchyPath,
                        normalizedSecondHierarchyPath,
                        treatCoincidenceAsInterference,
                        firstResolution,
                        secondResolution,
                        status: "targets_not_distinct",
                        failureReason: "Targeted static interference review requires two distinct component instances.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            LogStageCompleted(workflowName, "preconditions.distinct_targets");

            var checkedHierarchyPaths = new[]
            {
                firstResolution.ResolvedInstance.HierarchyPath,
                secondResolution.ResolvedInstance.HierarchyPath,
            };

            var interferenceCheck = ExecuteStage(
                workflowName,
                "verification.scope_check",
                () => _assembly.CheckInterference(checkedHierarchyPaths, treatCoincidenceAsInterference),
                check => ComposeDetail($"checkedComponentCount={check.CheckedComponentCount}", $"hasInterference={ToToken(check.HasInterference)}"));
            bool scopeEvaluatedAsRequested = interferenceCheck.CheckedComponentCount == checkedHierarchyPaths.Length;

            if (!scopeEvaluatedAsRequested)
            {
                LogStageFailed(
                    workflowName,
                    "verification.scope_check",
                    ComposeDetail($"reported={interferenceCheck.CheckedComponentCount}", $"expected={checkedHierarchyPaths.Length}"));
                return FinalizeWorkflow(
                    workflowName,
                    new TargetedStaticInterferenceReviewResult(
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
                        CompatibilityAdvisory: compatibilityAdvisory));
            }

            return FinalizeWorkflow(
                workflowName,
                new TargetedStaticInterferenceReviewResult(
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
                    CompatibilityAdvisory: compatibilityAdvisory));
        }
        catch (Exception ex)
        {
            LogUnhandledWorkflowException(workflowName, ex);
            throw;
        }
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
        const string workflowName = nameof(ReplaceNestedComponentAndVerifyPersistence);

        try
        {
            LogStageStarted(workflowName, "preconditions.replacement_file");
            if (string.IsNullOrWhiteSpace(replacementFilePath))
            {
                LogStageFailed(workflowName, "preconditions.replacement_file", "replacementFilePath must not be empty.");
                throw new ArgumentException("replacementFilePath must not be empty", nameof(replacementFilePath));
            }

            string normalizedReplacementFilePath = Path.GetFullPath(replacementFilePath);
            if (!File.Exists(normalizedReplacementFilePath))
            {
                LogStageFailed(workflowName, "preconditions.replacement_file", ComposeDetail($"path={normalizedReplacementFilePath}", "status=file_not_found"));
                throw new FileNotFoundException($"Replacement component file was not found: {normalizedReplacementFilePath}", normalizedReplacementFilePath);
            }

            LogStageCompleted(workflowName, "preconditions.replacement_file", ComposeDetail($"path={normalizedReplacementFilePath}"));

            LogStageStarted(workflowName, "preconditions.active_document");
            var activeDocument = _documents.GetActiveDocument();
            if (activeDocument == null)
            {
                LogStageFailed(workflowName, "preconditions.active_document", "status=no_active_document");
                throw new InvalidOperationException("No active document.");
            }

            LogStageCompleted(workflowName, "preconditions.active_document", ComposeDetail($"path={activeDocument.Path ?? activeDocument.Title}"));
            string? parentAssemblyFilePath = NormalizePathOrNull(activeDocument.Path);

            LogStageStarted(workflowName, "preconditions.target_resolution");
            var initialResolution = _assembly.ResolveComponentTarget(componentName, hierarchyPath, componentPath);
            if (!initialResolution.IsResolved || initialResolution.ResolvedInstance == null)
            {
                LogStageFailed(workflowName, "preconditions.target_resolution", ComposeDetail($"status={(initialResolution.IsAmbiguous ? "target_ambiguous" : "target_not_resolved")}"));
            }
            else
            {
                LogStageCompleted(workflowName, "preconditions.target_resolution", ComposeDetail($"hierarchyPath={initialResolution.ResolvedInstance.HierarchyPath}"));
            }

            var preReplacementImpact = ExecuteStage(
                workflowName,
                "preconditions.shared_part_impact",
                () => _assembly.AnalyzeSharedPartEditImpact(componentName, hierarchyPath, componentPath),
                impact => ComposeDetail($"affectedInstanceCount={impact.AffectedInstanceCount}", $"safeDirectEdit={ToToken(impact.SafeDirectEdit)}", $"recommendedAction={impact.RecommendedAction}"));
            var compatibilityAdvisory = CompatibilityPolicy.TryGetAdvisory(_connectionManager);

            LogStageStarted(workflowName, "preconditions.compatibility_gate");
            var compatibilityGate = CompatibilityPolicy.TryCreateHighRiskWorkflowGate(_connectionManager, "nested component replacement");
            if (compatibilityGate != null)
            {
                LogStageFailed(workflowName, "preconditions.compatibility_gate", ComposeDetail($"status={compatibilityGate.Status}", compatibilityGate.FailureReason));
                return FinalizeWorkflow(
                    workflowName,
                    CreateFailureResult(
                        initialResolution,
                        preReplacementImpact,
                        parentAssemblyFilePath,
                        normalizedReplacementFilePath,
                        status: compatibilityGate.Status,
                        failureReason: compatibilityGate.FailureReason,
                        compatibilityAdvisory: compatibilityGate.CompatibilityAdvisory));
            }

            LogStageCompleted(workflowName, "preconditions.compatibility_gate", "status=allowed");

            if (!initialResolution.IsResolved || initialResolution.ResolvedInstance == null)
            {
                return FinalizeWorkflow(
                    workflowName,
                    CreateFailureResult(
                        initialResolution,
                        preReplacementImpact,
                        parentAssemblyFilePath,
                        normalizedReplacementFilePath,
                        status: initialResolution.IsAmbiguous ? "target_ambiguous" : "target_not_resolved",
                        failureReason: initialResolution.IsAmbiguous
                            ? "The requested target matched multiple component instances."
                            : "The requested target could not be resolved to a single component instance.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            LogStageStarted(workflowName, "preconditions.target_validation");
            if (initialResolution.ResolvedInstance.Depth == 0 || string.IsNullOrWhiteSpace(initialResolution.OwningAssemblyHierarchyPath))
            {
                LogStageFailed(workflowName, "preconditions.target_validation", "status=target_not_nested");
                return FinalizeWorkflow(
                    workflowName,
                    CreateFailureResult(
                        initialResolution,
                        preReplacementImpact,
                        parentAssemblyFilePath,
                        normalizedReplacementFilePath,
                        status: "target_not_nested",
                        failureReason: "The resolved component is already top-level in the active assembly. Use the direct replace workflow instead.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            if (string.IsNullOrWhiteSpace(parentAssemblyFilePath))
            {
                LogStageFailed(workflowName, "preconditions.target_validation", "status=parent_assembly_not_saved");
                return FinalizeWorkflow(
                    workflowName,
                    CreateFailureResult(
                        initialResolution,
                        preReplacementImpact,
                        parentAssemblyFilePath,
                        normalizedReplacementFilePath,
                        status: "parent_assembly_not_saved",
                        failureReason: "The active parent assembly must be saved before persistence can be verified by reopening it.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            string? owningAssemblyFilePath = NormalizePathOrNull(initialResolution.OwningAssemblyFilePath);
            if (string.IsNullOrWhiteSpace(owningAssemblyFilePath))
            {
                LogStageFailed(workflowName, "preconditions.target_validation", "status=owning_assembly_not_saved");
                return FinalizeWorkflow(
                    workflowName,
                    CreateFailureResult(
                        initialResolution,
                        preReplacementImpact,
                        parentAssemblyFilePath,
                        normalizedReplacementFilePath,
                        status: "owning_assembly_not_saved",
                        failureReason: "The owning subassembly must be saved before nested replacement can be verified.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            string? sourceFilePath = NormalizePathOrNull(initialResolution.ResolvedInstance.Path);
            if (!string.IsNullOrWhiteSpace(sourceFilePath) && PathsEqual(sourceFilePath, normalizedReplacementFilePath))
            {
                LogStageFailed(workflowName, "preconditions.target_validation", "status=replacement_matches_source_file");
                return FinalizeWorkflow(
                    workflowName,
                    CreateFailureResult(
                        initialResolution,
                        preReplacementImpact,
                        parentAssemblyFilePath,
                        normalizedReplacementFilePath,
                        status: "replacement_matches_source_file",
                        failureReason: "The replacement file matches the currently resolved component source file, so the workflow would be a no-op.",
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            string replacementTargetHierarchyPath = GetReplacementTargetHierarchyPath(
                initialResolution.ResolvedInstance.HierarchyPath,
                initialResolution.OwningAssemblyHierarchyPath!);

            if (replacementTargetHierarchyPath.Contains('/'))
            {
                LogStageFailed(workflowName, "preconditions.target_validation", "status=target_not_top_level_in_owning_context");
                return FinalizeWorkflow(
                    workflowName,
                    CreateFailureResult(
                        initialResolution,
                        preReplacementImpact,
                        parentAssemblyFilePath,
                        normalizedReplacementFilePath,
                        status: "target_not_top_level_in_owning_context",
                        failureReason: "The resolved target is not a direct child of the owning assembly context.",
                        owningAssemblyHierarchyPath: initialResolution.OwningAssemblyHierarchyPath,
                        owningAssemblyFilePath: owningAssemblyFilePath,
                        replacementTargetHierarchyPath: replacementTargetHierarchyPath,
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            LogStageCompleted(
                workflowName,
                "preconditions.target_validation",
                ComposeDetail($"owningAssemblyFilePath={owningAssemblyFilePath}", $"replacementTargetHierarchyPath={replacementTargetHierarchyPath}"));

            ExecuteStage(
                workflowName,
                "mutation.activate_owning_assembly",
                () => _documents.OpenDocument(owningAssemblyFilePath),
                _ => ComposeDetail($"path={owningAssemblyFilePath}"));
            bool owningAssemblyActivated = IsActiveDocument(owningAssemblyFilePath);
            if (!owningAssemblyActivated)
            {
                LogStageFailed(workflowName, "mutation.activate_owning_assembly", "status=owning_assembly_not_active");
                return FinalizeWorkflow(
                    workflowName,
                    CreateFailureResult(
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
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            var replacementResult = ExecuteStage(
                workflowName,
                "mutation.replace_component",
                () => _assembly.ReplaceComponent(
                    replacementTargetHierarchyPath,
                    normalizedReplacementFilePath,
                    configName,
                    replaceAllInstances: false,
                    useConfigChoice,
                    reattachMates),
                result => ComposeDetail($"replacedHierarchyPath={result.ReplacedHierarchyPath}", $"success={ToToken(result.Success)}"));

            var saveResult = ExecuteStage(
                workflowName,
                "mutation.save_owning_assembly",
                () => _documents.SaveDocument(owningAssemblyFilePath),
                result => ComposeDetail($"outputPath={result.OutputPath}"));

            TryCloseDocument(parentAssemblyFilePath);
            TryCloseDocument(owningAssemblyFilePath);

            ExecuteStage(
                workflowName,
                "verification.parent_reload",
                () => _documents.OpenDocument(parentAssemblyFilePath),
                _ => ComposeDetail($"path={parentAssemblyFilePath}"));
            bool parentAssemblyReloaded = IsActiveDocument(parentAssemblyFilePath);
            if (!parentAssemblyReloaded)
            {
                LogStageFailed(workflowName, "verification.parent_reload", "status=parent_assembly_not_reloaded");
                return FinalizeWorkflow(
                    workflowName,
                    CreateFailureResult(
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
                        compatibilityAdvisory: compatibilityAdvisory));
            }

            var persistenceResolution = ExecuteStage(
                workflowName,
                "verification.persistence_resolution",
                () => ResolvePersistedTarget(initialResolution, normalizedReplacementFilePath),
                resolution => ComposeDetail($"resolved={ToToken(resolution.IsResolved)}", $"reuseCount={resolution.SourceFileReuseCount}"));
            SharedPartEditImpactResult? postReplacementImpact = null;
            if (persistenceResolution.IsResolved)
            {
                postReplacementImpact = ExecuteStage(
                    workflowName,
                    "verification.post_replacement_shared_part_impact",
                    () => _assembly.AnalyzeSharedPartEditImpact(hierarchyPath: persistenceResolution.ResolvedInstance!.HierarchyPath),
                    impact => ComposeDetail($"affectedInstanceCount={impact.AffectedInstanceCount}", $"safeDirectEdit={ToToken(impact.SafeDirectEdit)}"));
            }
            else
            {
                LogStageSkipped(workflowName, "verification.post_replacement_shared_part_impact", "persistence target not resolved");
            }

            bool persistenceVerified = persistenceResolution.IsResolved
                && persistenceResolution.ResolvedInstance != null
                && PathsEqual(persistenceResolution.ResolvedInstance.Path, normalizedReplacementFilePath);

            if (!persistenceVerified)
            {
                LogStageFailed(workflowName, "verification.persistence_resolution", "status=persistence_verification_failed");
            }

            return FinalizeWorkflow(
                workflowName,
                new NestedComponentReplacementWorkflowResult(
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
                    compatibilityAdvisory));
        }
        catch (Exception ex)
        {
            LogUnhandledWorkflowException(workflowName, ex);
            throw;
        }
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

    private T ExecuteStage<T>(string workflowName, string stageName, Func<T> action, Func<T, string?>? detailFactory = null)
    {
        LogStageStarted(workflowName, stageName);

        try
        {
            var result = action();
            LogStageCompleted(workflowName, stageName, detailFactory?.Invoke(result));
            return result;
        }
        catch (Exception ex)
        {
            LogStageFailed(workflowName, stageName, ex.Message);
            throw;
        }
    }

    private ActiveDocumentHealthDiagnosticsResult FinalizeWorkflow(string workflowName, ActiveDocumentHealthDiagnosticsResult result)
    {
        _workflowStageLogger.LogStage(
            workflowName,
            "final",
            string.Equals(result.Status, "completed", StringComparison.OrdinalIgnoreCase) ? "completed" : "failed",
            ComposeDetail(
                $"status={result.Status}",
                result.ActionableDiagnostics == null ? null : $"actionableIssues={result.ActionableDiagnostics.CurrentIssues.Count}",
                result.ActionableDiagnostics == null ? null : $"resolvedByRebuild={result.ActionableDiagnostics.ResolvedByRebuildIssues.Count}",
                result.FailureReason));
        return result;
    }

    private TargetedStaticInterferenceReviewResult FinalizeWorkflow(string workflowName, TargetedStaticInterferenceReviewResult result)
    {
        _workflowStageLogger.LogStage(
            workflowName,
            "final",
            string.Equals(result.Status, "completed", StringComparison.OrdinalIgnoreCase) ? "completed" : "failed",
            ComposeDetail($"status={result.Status}", result.FailureReason));
        return result;
    }

    private NestedComponentReplacementWorkflowResult FinalizeWorkflow(string workflowName, NestedComponentReplacementWorkflowResult result)
    {
        _workflowStageLogger.LogStage(
            workflowName,
            "final",
            string.Equals(result.Status, "completed", StringComparison.OrdinalIgnoreCase) ? "completed" : "failed",
            ComposeDetail($"status={result.Status}", result.FailureReason));
        return result;
    }

    private void LogUnhandledWorkflowException(string workflowName, Exception ex)
    {
        _workflowStageLogger.LogStage(workflowName, "final", "failed", ComposeDetail("status=exception", ex.Message));
    }

    private void LogStageStarted(string workflowName, string stageName, string? detail = null) =>
        _workflowStageLogger.LogStage(workflowName, stageName, "started", detail);

    private void LogStageCompleted(string workflowName, string stageName, string? detail = null) =>
        _workflowStageLogger.LogStage(workflowName, stageName, "completed", detail);

    private void LogStageFailed(string workflowName, string stageName, string? detail = null) =>
        _workflowStageLogger.LogStage(workflowName, stageName, "failed", detail);

    private void LogStageSkipped(string workflowName, string stageName, string? detail = null) =>
        _workflowStageLogger.LogStage(workflowName, stageName, "skipped", detail);

    private static string? ComposeDetail(params string?[] parts)
    {
        var rendered = parts
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return rendered.Length == 0 ? null : string.Join(" | ", rendered);
    }

    private static string ToToken(bool value) => value ? "true" : "false";

    private static int GetCorrelatedIssueCount(FeatureDiagnosticsResult diagnostics) =>
        diagnostics.CorrelatedIssues?.Count ?? 0;

    private static DocumentHealthActionableDiagnosticsInfo? CreateActionableDiagnostics(
        FeatureDiagnosticsResult? featureDiagnosticsBeforeRebuild,
        FeatureDiagnosticsResult? featureDiagnosticsAfterRebuild)
    {
        if (featureDiagnosticsBeforeRebuild == null && featureDiagnosticsAfterRebuild == null)
        {
            return null;
        }

        var beforeIssues = GetCorrelatedIssues(featureDiagnosticsBeforeRebuild);
        var afterIssues = GetCorrelatedIssues(featureDiagnosticsAfterRebuild);

        return new DocumentHealthActionableDiagnosticsInfo(
            CurrentIssues: afterIssues,
            BlockingIssues: afterIssues.Where(static issue => !issue.IsWarning).ToArray(),
            WarningIssues: afterIssues.Where(static issue => issue.IsWarning).ToArray(),
            ResolvedByRebuildIssues: FindDiagnosticIssueDelta(beforeIssues, afterIssues),
            IntroducedByRebuildIssues: FindDiagnosticIssueDelta(afterIssues, beforeIssues));
    }

    private static IReadOnlyList<CorrelatedDiagnosticIssueInfo> GetCorrelatedIssues(FeatureDiagnosticsResult? diagnostics) =>
        diagnostics?.CorrelatedIssues?.ToArray() ?? Array.Empty<CorrelatedDiagnosticIssueInfo>();

    private static IReadOnlyList<CorrelatedDiagnosticIssueInfo> FindDiagnosticIssueDelta(
        IReadOnlyList<CorrelatedDiagnosticIssueInfo> baseline,
        IReadOnlyList<CorrelatedDiagnosticIssueInfo> comparison)
    {
        if (baseline.Count == 0)
        {
            return Array.Empty<CorrelatedDiagnosticIssueInfo>();
        }

        if (comparison.Count == 0)
        {
            return baseline.ToArray();
        }

        var remaining = comparison
            .Select(CreateDiagnosticCorrelationKey)
            .ToList();
        var delta = new List<CorrelatedDiagnosticIssueInfo>();

        foreach (var issue in baseline)
        {
            var key = CreateDiagnosticCorrelationKey(issue);
            int matchingIndex = remaining.FindIndex(existing => existing == key);
            if (matchingIndex >= 0)
            {
                remaining.RemoveAt(matchingIndex);
                continue;
            }

            delta.Add(issue);
        }

        return delta.Count == 0
            ? Array.Empty<CorrelatedDiagnosticIssueInfo>()
            : delta.ToArray();
    }

    private static DiagnosticCorrelationKey CreateDiagnosticCorrelationKey(CorrelatedDiagnosticIssueInfo issue)
    {
        string matchingInstancesKey = string.Join(
            "|",
            issue.TargetContext.MatchingInstances
                .OrderBy(instance => instance.HierarchyPath, StringComparer.OrdinalIgnoreCase)
                .Select(instance => $"{instance.HierarchyPath}>{instance.Name}>{NormalizePathOrNull(instance.Path) ?? string.Empty}"));

        return new DiagnosticCorrelationKey(
            Name: issue.Name,
            TypeName: issue.TypeName,
            ErrorCode: issue.ErrorCode,
            IsWarning: issue.IsWarning,
            ErrorName: issue.ErrorName,
            ErrorDescription: issue.ErrorDescription,
            ScopeType: issue.TargetContext.ScopeType,
            IsExact: issue.TargetContext.IsExact,
            IsAmbiguous: issue.TargetContext.IsAmbiguous,
            DocumentPath: NormalizePathOrNull(issue.TargetContext.DocumentPath),
            HierarchyPath: issue.TargetContext.HierarchyPath,
            ComponentName: issue.TargetContext.ComponentName,
            SourceFilePath: NormalizePathOrNull(issue.TargetContext.SourceFilePath),
            OwningAssemblyHierarchyPath: issue.TargetContext.OwningAssemblyHierarchyPath,
            OwningAssemblyFilePath: NormalizePathOrNull(issue.TargetContext.OwningAssemblyFilePath),
            SourceFileReuseCount: issue.TargetContext.SourceFileReuseCount,
            Reason: issue.TargetContext.Reason,
            MatchingInstancesKey: matchingInstancesKey);
    }

    private sealed record DiagnosticCorrelationKey(
        string Name,
        string TypeName,
        int ErrorCode,
        bool IsWarning,
        string ErrorName,
        string ErrorDescription,
        string ScopeType,
        bool IsExact,
        bool IsAmbiguous,
        string? DocumentPath,
        string? HierarchyPath,
        string? ComponentName,
        string? SourceFilePath,
        string? OwningAssemblyHierarchyPath,
        string? OwningAssemblyFilePath,
        int SourceFileReuseCount,
        string? Reason,
        string MatchingInstancesKey);

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
            CompatibilityAdvisory: compatibilityAdvisory,
            ActionableDiagnostics: CreateActionableDiagnostics(featureDiagnosticsBeforeRebuild, featureDiagnosticsAfterRebuild));
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
