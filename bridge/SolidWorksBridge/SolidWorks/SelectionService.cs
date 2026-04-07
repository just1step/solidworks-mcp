using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SolidWorksBridge.SolidWorks;

/// <summary>
/// Result of a selection operation.
/// </summary>
public record SelectionResult(
    bool Success,
    string Message,
    SelectableEntityType? EntityType = null,
    int? EntityIndex = null,
    string? ComponentName = null,
    double[]? Box = null);

/// <summary>
/// Reference plane metadata discovered from the active document feature tree.
/// </summary>
public record ReferencePlaneInfo(
    int Index,
    string Name,
    string SelectionName,
    string SelectionType);

/// <summary>
/// Small snapshot of the current SolidWorks language and active-document planes.
/// </summary>
public record SolidWorksContextInfo(
    string CurrentLanguage,
    IReadOnlyList<ReferencePlaneInfo> ReferencePlanes);

/// <summary>
/// Lightweight description of a top-level FeatureManager node.
/// </summary>
public record FeatureTreeItemInfo(
    int Index,
    string Name,
    string TypeName,
    bool IsSketch,
    bool HasChildren);

/// <summary>
/// Diagnostic state for one FeatureManager node based on SolidWorks feature error codes.
/// </summary>
public record FeatureDiagnosticInfo(
    int Index,
    string Name,
    string TypeName,
    bool IsSketch,
    bool HasChildren,
    bool HasIssue,
    int ErrorCode,
    bool IsWarning,
    string ErrorName,
    string ErrorDescription,
    bool AppearsInWhatsWrong,
    DiagnosticTargetContextInfo? TargetContext = null);

/// <summary>
/// Resolved ownership context for one diagnostic item when the active document is an assembly.
/// </summary>
public record DiagnosticTargetContextInfo(
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
    IReadOnlyList<ComponentInstanceInfo> MatchingInstances);

/// <summary>
/// One issue-bearing diagnostic item enriched with assembly target context.
/// </summary>
public record CorrelatedDiagnosticIssueInfo(
    string Source,
    int? Index,
    string Name,
    string TypeName,
    int ErrorCode,
    bool IsWarning,
    string ErrorName,
    string ErrorDescription,
    bool AppearsInWhatsWrong,
    DiagnosticTargetContextInfo TargetContext);

/// <summary>
/// One item from SolidWorks' What's Wrong system for the active document.
/// </summary>
public record WhatsWrongItemInfo(
    string Name,
    string TypeName,
    int ErrorCode,
    bool IsWarning,
    string ErrorName,
    string ErrorDescription,
    DiagnosticTargetContextInfo? TargetContext = null);

/// <summary>
/// Combined feature-tree and What's Wrong diagnostics for the active document.
/// </summary>
public record FeatureDiagnosticsResult(
    IReadOnlyList<FeatureDiagnosticInfo> FeatureDiagnostics,
    IReadOnlyList<WhatsWrongItemInfo> WhatsWrongItems,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<CorrelatedDiagnosticIssueInfo>? CorrelatedIssues = null);

/// <summary>
/// Lightweight description of whether the active document is currently in an edit mode
/// that blocks safe feature-tree reads or delete operations.
/// </summary>
public record EditStateInfo(
    bool IsEditing,
    string EditMode,
    bool CanReadFeatureTree,
    bool CanDeleteFeatures);

/// <summary>
/// Result of deleting one or more features from the active document.
/// </summary>
public record DeleteFeaturesResult(
    int DeletedCount,
    IReadOnlyList<string> DeletedFeatureNames,
    IReadOnlyList<string> FailedFeatureNames);

/// <summary>
/// Supported selectable topology entity kinds.
/// </summary>
public enum SelectableEntityType
{
    Face,
    Edge,
    Vertex,
}

/// <summary>
/// Lightweight description of a selectable topology entity.
/// </summary>
public record SelectableEntityInfo(
    int Index,
    SelectableEntityType EntityType,
    string? ComponentName,
    double[]? Box);

/// <summary>
/// Stable reference to a measured topology entity.
/// </summary>
public record MeasuredEntityInfo(
    SelectableEntityType EntityType,
    int Index,
    string? ComponentName,
    double[]? Box);

/// <summary>
/// Result of measuring two topology entities using SolidWorks' official IMeasure API.
/// Distances are reported in meters when available; unavailable values are null.
/// </summary>
public record EntityMeasurementResult(
    MeasuredEntityInfo FirstEntity,
    MeasuredEntityInfo SecondEntity,
    int ArcOption,
    double? Distance,
    double? NormalDistance,
    double? CenterDistance,
    double? Angle,
    double? DeltaX,
    double? DeltaY,
    double? DeltaZ,
    double? Projection,
    double? X,
    double? Y,
    double? Z,
    bool IsParallel,
    bool IsPerpendicular,
    bool IsIntersect);

/// <summary>
/// Interface for selecting entities in the active document.
/// All sketch / feature / assembly operations depend on prior selection.
/// </summary>
public interface ISelectionService
{
    /// <summary>
    /// Select an entity by its name and type string (e.g. "Front Plane", "swSelDATUMPLANES").
    /// Coordinates default to 0,0,0 which is sufficient for named entities.
    /// </summary>
    SelectionResult SelectByName(string name, string selType);

    /// <summary>
    /// List selectable topology entities on the active part or assembly.
    /// </summary>
    IReadOnlyList<SelectableEntityInfo> ListEntities(
        SelectableEntityType? entityType = null,
        string? componentName = null);

    /// <summary>
    /// List reference planes from the active document by traversing the feature tree.
    /// The returned names are localized to the current SolidWorks language.
    /// </summary>
    IReadOnlyList<ReferencePlaneInfo> ListReferencePlanes();

    /// <summary>
    /// Get the current SolidWorks UI language and the active document's reference planes.
    /// If no document is open, the plane list is empty.
    /// </summary>
    SolidWorksContextInfo GetSolidWorksContext();

    /// <summary>
    /// Enumerate the active document's top-level FeatureManager nodes.
    /// </summary>
    IReadOnlyList<FeatureTreeItemInfo> ListFeatureTree();

    /// <summary>
    /// Read SolidWorks feature error codes and What's Wrong items for the active document.
    /// </summary>
    FeatureDiagnosticsResult GetFeatureDiagnostics();

    /// <summary>
    /// Report whether the active document is currently editing a sketch or is otherwise in a safe state
    /// for feature-tree reads and delete operations.
    /// </summary>
    EditStateInfo GetEditState();

    /// <summary>
    /// Select a topology entity by the index returned from <see cref="ListEntities"/>.
    /// </summary>
    SelectionResult SelectEntity(
        SelectableEntityType entityType,
        int index,
        bool append = false,
        int mark = 0,
        string? componentName = null);

    /// <summary>
    /// Measure two topology entities by their ListEntities indexes using SolidWorks' official IMeasure API.
    /// </summary>
    EntityMeasurementResult MeasureEntities(
        SelectableEntityType firstEntityType,
        int firstIndex,
        SelectableEntityType secondEntityType,
        int secondIndex,
        string? firstComponentName = null,
        string? secondComponentName = null,
        int arcOption = 1);

    /// <summary>
    /// Delete a top-level feature or sketch by its feature-tree name.
    /// </summary>
    SelectionResult DeleteFeatureByName(string featureName);

    /// <summary>
    /// Delete loose sketches that are present in the FeatureManager but are not consumed by downstream features.
    /// </summary>
    DeleteFeaturesResult DeleteUnusedSketches();

    /// <summary>Clear the current selection set.</summary>
    void ClearSelection();
}

/// <summary>
/// Implements <see cref="ISelectionService"/> via <see cref="ISwConnectionManager"/>.
/// </summary>
public class SelectionService : ISelectionService
{
    private enum StandardPlaneKind
    {
        Unknown = 0,
        Front,
        Top,
        Right,
    }

    private static readonly IReadOnlyDictionary<string, string[]> SelectionTypeAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["swSelDATUMPLANES"] = ["swSelDATUMPLANES", "PLANE"],
            ["PLANE"] = ["PLANE", "swSelDATUMPLANES"],
            ["swSelFACES"] = ["swSelFACES", "FACE"],
            ["FACE"] = ["FACE", "swSelFACES"],
            ["swSelEDGES"] = ["swSelEDGES", "EDGE"],
            ["EDGE"] = ["EDGE", "swSelEDGES"],
            ["swSelVERTICES"] = ["swSelVERTICES", "VERTEX"],
            ["VERTEX"] = ["VERTEX", "swSelVERTICES"],
        };

    private sealed record EntityCandidate(
        int Index,
        IEntity Entity,
        SelectableEntityType EntityType,
        string? ComponentName,
        double[]? Box);

    private sealed record FeatureErrorInfo(int ErrorCode, bool IsWarning, SwCodeInfo CodeInfo);

    private sealed record FeatureNode(
        Feature Feature,
        int Index,
        string Name,
        string TypeName,
        bool IsSketch,
        bool HasChildren);

    private sealed record WhatsWrongFeatureInfo(
        Feature Feature,
        string Name,
        string TypeName,
        int ErrorCode,
        bool IsWarning,
        SwCodeInfo CodeInfo);

    private sealed record AssemblyComponentContext(
        IComponent2 Component,
        ComponentInstanceInfo Info);

    private readonly ISwConnectionManager _cm;

    public SelectionService(ISwConnectionManager cm)
    {
        _cm = cm ?? throw new ArgumentNullException(nameof(cm));
    }

    public SelectionResult SelectByName(string name, string selType)
    {
        _cm.EnsureConnected();
        var doc = GetActiveModelDoc();
        doc.ClearSelection2(true);

        foreach (var candidateType in ExpandSelectionTypes(selType))
        {
            // SelectByID(name, type, x, y, z) — x/y/z are 0,0,0 for named geometry
            bool ok = doc.SelectByID(name, candidateType, 0, 0, 0);
            if (ok)
            {
                string message = string.Equals(candidateType, selType, StringComparison.OrdinalIgnoreCase)
                    ? $"Selected '{name}'"
                    : $"Selected '{name}' using selection type '{candidateType}' (requested '{selType}')";
                return new SelectionResult(true, message);
            }
        }

        // Localized SolidWorks installs may rename the default planes.
        // For plane selections, retry by matching semantic plane kind (front/top/right)
        // against discovered reference planes from the active feature tree.
        if (IsPlaneSelection(selType))
        {
            var fallback = TrySelectLocalizedStandardPlane(doc, name, selType);
            if (fallback != null)
            {
                return fallback;
            }
        }

        return new SelectionResult(false, $"Could not select '{name}' (type: {selType})");
    }

    public IReadOnlyList<SelectableEntityInfo> ListEntities(
        SelectableEntityType? entityType = null,
        string? componentName = null)
    {
        _cm.EnsureConnected();

        return EnumerateEntities(entityType, componentName)
            .Select(candidate => new SelectableEntityInfo(
                candidate.Index,
                candidate.EntityType,
                candidate.ComponentName,
                candidate.Box))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ReferencePlaneInfo> ListReferencePlanes()
    {
        _cm.EnsureConnected();
        var doc = GetActiveModelDoc();
        return EnumerateReferencePlanes(doc).ToList().AsReadOnly();
    }

    public SolidWorksContextInfo GetSolidWorksContext()
    {
        _cm.EnsureConnected();

        var swApp = _cm.SwApp ?? throw new InvalidOperationException("SolidWorks connection is not available.");
        string language = SafeGetCurrentLanguage(swApp) ?? "unknown";
        var doc = swApp.IActiveDoc2;
        IReadOnlyList<ReferencePlaneInfo> planes = doc == null
            ? Array.Empty<ReferencePlaneInfo>()
            : EnumerateReferencePlanes(doc).ToList().AsReadOnly();

        return new SolidWorksContextInfo(language, planes);
    }

    public IReadOnlyList<FeatureTreeItemInfo> ListFeatureTree()
    {
        _cm.EnsureConnected();
        var doc = GetActiveModelDoc();
        EnsureNotEditing(doc, "reading the FeatureManager tree");

        return EnumerateFeatureTree(doc)
            .Select(node => new FeatureTreeItemInfo(
                node.Index,
                node.Name,
                node.TypeName,
                node.IsSketch,
                node.HasChildren))
            .ToList()
            .AsReadOnly();
    }

    public FeatureDiagnosticsResult GetFeatureDiagnostics()
    {
        _cm.EnsureConnected();
        var doc = GetActiveModelDoc();
        EnsureNotEditing(doc, "reading feature diagnostics");

        var treeNodes = EnumerateFeatureTree(doc).ToList();
        var assemblyComponents = TryEnumerateAssemblyComponentContexts(doc);
        string? activeDocumentPath = NormalizePathOrNull(doc.GetPathName());
        var whatsWrongEntries = EnumerateWhatsWrongItems(doc).ToList();
        var whatsWrongItems = whatsWrongEntries
            .Select(entry => new WhatsWrongItemInfo(
                entry.Name,
                entry.TypeName,
                entry.ErrorCode,
                entry.IsWarning,
                entry.CodeInfo.Name,
                entry.CodeInfo.Description,
                CorrelateDiagnosticTargetContext(
                    entry.Feature,
                    entry.Name,
                    entry.TypeName,
                    activeDocumentPath,
                    assemblyComponents)))
            .ToList();
        var whatsWrongKeys = new HashSet<string>(
            whatsWrongItems.Select(item => BuildFeatureKey(item.Name, item.TypeName)),
            StringComparer.OrdinalIgnoreCase);

        var featureDiagnostics = treeNodes
            .Select(node =>
            {
                var error = GetFeatureErrorInfo(node.Feature);
                bool appearsInWhatsWrong = whatsWrongKeys.Contains(BuildFeatureKey(node.Name, node.TypeName));
                DiagnosticTargetContextInfo? targetContext = error.ErrorCode != 0 || appearsInWhatsWrong
                    ? CorrelateDiagnosticTargetContext(
                        node.Feature,
                        node.Name,
                        node.TypeName,
                        activeDocumentPath,
                        assemblyComponents)
                    : null;

                return new FeatureDiagnosticInfo(
                    node.Index,
                    node.Name,
                    node.TypeName,
                    node.IsSketch,
                    node.HasChildren,
                    HasIssue: error.ErrorCode != 0,
                    ErrorCode: error.ErrorCode,
                    IsWarning: error.IsWarning,
                    ErrorName: error.CodeInfo.Name,
                    ErrorDescription: error.CodeInfo.Description,
                    AppearsInWhatsWrong: appearsInWhatsWrong,
                    TargetContext: targetContext);
            })
            .ToList()
            .AsReadOnly();

        int warningCount = whatsWrongItems.Count(item => item.IsWarning);
        int errorCount = whatsWrongItems.Count - warningCount;
        var correlatedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var correlatedIssues = new List<CorrelatedDiagnosticIssueInfo>();

        foreach (var item in featureDiagnostics.Where(item => item.TargetContext != null))
        {
            correlatedIssues.Add(new CorrelatedDiagnosticIssueInfo(
                Source: "feature_tree",
                Index: item.Index,
                Name: item.Name,
                TypeName: item.TypeName,
                ErrorCode: item.ErrorCode,
                IsWarning: item.IsWarning,
                ErrorName: item.ErrorName,
                ErrorDescription: item.ErrorDescription,
                AppearsInWhatsWrong: item.AppearsInWhatsWrong,
                TargetContext: item.TargetContext!));
            correlatedKeys.Add(BuildFeatureKey(item.Name, item.TypeName));
        }

        foreach (var item in whatsWrongItems.Where(item => item.TargetContext != null))
        {
            if (!correlatedKeys.Add(BuildFeatureKey(item.Name, item.TypeName)))
            {
                continue;
            }

            correlatedIssues.Add(new CorrelatedDiagnosticIssueInfo(
                Source: "whats_wrong",
                Index: null,
                Name: item.Name,
                TypeName: item.TypeName,
                ErrorCode: item.ErrorCode,
                IsWarning: item.IsWarning,
                ErrorName: item.ErrorName,
                ErrorDescription: item.ErrorDescription,
                AppearsInWhatsWrong: true,
                TargetContext: item.TargetContext!));
        }

        return new FeatureDiagnosticsResult(
            featureDiagnostics,
            whatsWrongItems.AsReadOnly(),
            errorCount,
            warningCount,
            correlatedIssues.AsReadOnly());
    }

    public EditStateInfo GetEditState()
    {
        _cm.EnsureConnected();
        var doc = GetActiveModelDoc();
        return GetEditState(doc);
    }

    public SelectionResult SelectEntity(
        SelectableEntityType entityType,
        int index,
        bool append = false,
        int mark = 0,
        string? componentName = null)
    {
        _cm.EnsureConnected();

        var candidate = EnumerateEntities(entityType, componentName)
            .FirstOrDefault(item => item.Index == index);

        if (candidate == null)
        {
            string scope = string.IsNullOrWhiteSpace(componentName)
                ? string.Empty
                : $" for component '{componentName}'";
            return new SelectionResult(false, $"Could not find {entityType} at index {index}{scope}");
        }

        var selectionManager = GetActiveModelDoc().ISelectionManager
            ?? throw new InvalidOperationException("No selection manager available.");

        if (!append)
        {
            GetActiveModelDoc().ClearSelection2(true);
        }

        var selectData = CreateSelectData(selectionManager, mark);

        bool ok = candidate.Entity.Select4(append, selectData);
        return ok
            ? new SelectionResult(
                true,
                $"Selected {entityType} at index {index}",
                candidate.EntityType,
                candidate.Index,
                candidate.ComponentName,
                candidate.Box)
            : new SelectionResult(
                false,
                $"Failed to select {entityType} at index {index}",
                candidate.EntityType,
                candidate.Index,
                candidate.ComponentName,
                candidate.Box);
    }

    public EntityMeasurementResult MeasureEntities(
        SelectableEntityType firstEntityType,
        int firstIndex,
        SelectableEntityType secondEntityType,
        int secondIndex,
        string? firstComponentName = null,
        string? secondComponentName = null,
        int arcOption = 1)
    {
        if (arcOption is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(arcOption), "arcOption must be 0 (center), 1 (minimum), or 2 (maximum).");
        }

        _cm.EnsureConnected();

        var doc = GetActiveModelDoc();
        var first = ResolveEntityCandidate(firstEntityType, firstIndex, firstComponentName);
        var second = ResolveEntityCandidate(secondEntityType, secondIndex, secondComponentName);
        var selectionManager = doc.ISelectionManager
            ?? throw new InvalidOperationException("No selection manager available.");
        var extension = doc.Extension
            ?? throw new InvalidOperationException("No model extension available on the active document.");

        doc.ClearSelection2(true);
        try
        {
            if (!first.Entity.Select4(false, CreateSelectData(selectionManager, 0)))
            {
                throw new InvalidOperationException($"Failed to select the first entity ({firstEntityType} index {firstIndex}).");
            }

            if (!second.Entity.Select4(true, CreateSelectData(selectionManager, 0)))
            {
                throw new InvalidOperationException($"Failed to select the second entity ({secondEntityType} index {secondIndex}).");
            }

            var measure = extension.CreateMeasure()
                ?? throw new InvalidOperationException("SolidWorks did not create a measure tool.");
            measure.ArcOption = arcOption;

            if (!measure.Calculate(null))
            {
                throw new InvalidOperationException("SolidWorks could not measure the selected entities.");
            }

            return new EntityMeasurementResult(
                ToMeasuredEntityInfo(first),
                ToMeasuredEntityInfo(second),
                arcOption,
                NormalizeMeasureValue(measure.Distance),
                NormalizeMeasureValue(measure.NormalDistance),
                NormalizeMeasureValue(measure.CenterDistance),
                NormalizeMeasureValue(measure.Angle),
                NormalizeMeasureValue(measure.DeltaX),
                NormalizeMeasureValue(measure.DeltaY),
                NormalizeMeasureValue(measure.DeltaZ),
                NormalizeMeasureValue(measure.Projection),
                NormalizeMeasureValue(measure.X),
                NormalizeMeasureValue(measure.Y),
                NormalizeMeasureValue(measure.Z),
                measure.IsParallel,
                measure.IsPerpendicular,
                measure.IsIntersect);
        }
        finally
        {
            doc.ClearSelection2(true);
        }
    }

    public SelectionResult DeleteFeatureByName(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            throw new ArgumentException("featureName must not be empty", nameof(featureName));
        }

        _cm.EnsureConnected();
        var doc = GetActiveModelDoc();
        EnsureNotEditing(doc, $"deleting feature '{featureName}'");
        var feature = EnumerateFeatureTree(doc)
            .FirstOrDefault(node => string.Equals(node.Name, featureName, StringComparison.OrdinalIgnoreCase));

        if (feature == null)
        {
            return new SelectionResult(false, $"Could not find feature '{featureName}' in the FeatureManager tree.");
        }

        return TryDeleteFeature(doc, feature.Feature)
            ? new SelectionResult(true, $"Deleted feature '{feature.Name}'.")
            : new SelectionResult(false, $"Failed to delete feature '{feature.Name}'.");
    }

    public DeleteFeaturesResult DeleteUnusedSketches()
    {
        _cm.EnsureConnected();
        var doc = GetActiveModelDoc();
        EnsureNotEditing(doc, "deleting unused sketches");

        var deleted = new List<string>();
        var failed = new List<string>();
        var looseSketches = EnumerateFeatureTree(doc)
            .Where(node => node.IsSketch && !node.HasChildren)
            .OrderByDescending(node => node.Index)
            .ToList();

        foreach (var sketch in looseSketches)
        {
            if (TryDeleteFeature(doc, sketch.Feature))
            {
                deleted.Add(sketch.Name);
            }
            else
            {
                failed.Add(sketch.Name);
            }
        }

        return new DeleteFeaturesResult(deleted.Count, deleted.AsReadOnly(), failed.AsReadOnly());
    }

    public void ClearSelection()
    {
        _cm.EnsureConnected();
        GetActiveModelDoc().ClearSelection2(true);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private IModelDoc2 GetActiveModelDoc()
    {
        return _cm.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException("No active document. Open or create a document first.");
    }

    private IEnumerable<EntityCandidate> EnumerateEntities(
        SelectableEntityType? entityType,
        string? componentName)
    {
        var all = EnumerateBodyContexts()
            .SelectMany(context => EnumerateEntitiesForBody(context.Body, context.ComponentName))
            .Where(candidate => entityType == null || candidate.EntityType == entityType)
            .Where(candidate => string.IsNullOrWhiteSpace(componentName)
                || string.Equals(candidate.ComponentName, componentName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        for (int index = 0; index < all.Count; index++)
        {
            var candidate = all[index];
            yield return candidate with { Index = index };
        }
    }

    private EntityCandidate ResolveEntityCandidate(
        SelectableEntityType entityType,
        int index,
        string? componentName)
    {
        var candidate = EnumerateEntities(entityType, componentName)
            .FirstOrDefault(item => item.Index == index);

        if (candidate == null)
        {
            string scope = string.IsNullOrWhiteSpace(componentName)
                ? string.Empty
                : $" for component '{componentName}'";
            throw new InvalidOperationException($"Could not find {entityType} at index {index}{scope}.");
        }

        return candidate;
    }

    private static MeasuredEntityInfo ToMeasuredEntityInfo(EntityCandidate candidate)
        => new(candidate.EntityType, candidate.Index, candidate.ComponentName, candidate.Box);

    private static IEnumerable<ReferencePlaneInfo> EnumerateReferencePlanes(IModelDoc2 doc)
    {
        int index = 0;
        for (var feature = doc.FirstFeature() as Feature; feature != null; feature = feature.GetNextFeature() as Feature)
        {
            string? typeName = SafeGetFeatureTypeName(feature);
            if (!string.Equals(typeName, "RefPlane", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var (selectionName, selectionType) = SafeGetSelectionIdentity(feature);
            string name = SafeGetFeatureName(feature)
                ?? selectionName
                ?? $"RefPlane{index + 1}";

            yield return new ReferencePlaneInfo(
                index,
                name,
                selectionName ?? name,
                selectionType ?? "PLANE");

            index++;
        }
    }

    private static IEnumerable<FeatureNode> EnumerateFeatureTree(IModelDoc2 doc)
    {
        int index = 0;
        for (var feature = doc.FirstFeature() as Feature; feature != null; feature = feature.GetNextFeature() as Feature)
        {
            string typeName = SafeGetFeatureTypeName(feature) ?? "Unknown";
            string name = SafeGetFeatureName(feature)
                ?? $"Feature{index + 1}";

            yield return new FeatureNode(
                feature,
                index,
                name,
                typeName,
                IsSketchLike(typeName),
                HasChildFeatures(feature));

            index++;
        }
    }

    private static IEnumerable<WhatsWrongFeatureInfo> EnumerateWhatsWrongItems(IModelDoc2 doc)
    {
        var extension = doc.Extension;
        if (extension == null)
        {
            yield break;
        }

        object featuresRaw;
        object errorCodesRaw;
        object warningsRaw;
        if (!extension.GetWhatsWrong(out featuresRaw, out errorCodesRaw, out warningsRaw))
        {
            yield break;
        }

        var features = (featuresRaw as object[] ?? Array.Empty<object>()).OfType<Feature>().ToArray();
        var errorCodes = ToIntArray(errorCodesRaw);
        var warnings = ToBoolArray(warningsRaw);
        int count = new[] { features.Length, errorCodes.Length, warnings.Length }.Min();
        for (int index = 0; index < count; index++)
        {
            var feature = features[index];
            int errorCode = errorCodes[index];
            var codeInfo = SolidWorksApiErrorFactory.CreateFeatureErrorCodeInfo(errorCode);
            yield return new WhatsWrongFeatureInfo(
                feature,
                Name: SafeGetFeatureName(feature) ?? $"Feature{index + 1}",
                TypeName: SafeGetFeatureTypeName(feature) ?? "Unknown",
                ErrorCode: errorCode,
                IsWarning: warnings[index],
                CodeInfo: codeInfo);
        }
    }

    private static IReadOnlyList<AssemblyComponentContext>? TryEnumerateAssemblyComponentContexts(IModelDoc2 doc)
    {
        if (doc is not IAssemblyDoc assembly)
        {
            return null;
        }

        var raw = assembly.GetComponents(true) as object[] ?? Array.Empty<object>();
        var results = new List<AssemblyComponentContext>();
        foreach (var component in raw.OfType<IComponent2>())
        {
            TraverseAssemblyComponent(component, component.Name2 ?? "Component", depth: 0, results);
        }

        return results.AsReadOnly();
    }

    private static void TraverseAssemblyComponent(
        IComponent2 component,
        string hierarchyPath,
        int depth,
        ICollection<AssemblyComponentContext> results)
    {
        string name = component.Name2 ?? $"Component{depth}";
        string path = component.GetPathName() ?? string.Empty;
        results.Add(new AssemblyComponentContext(component, new ComponentInstanceInfo(name, path, hierarchyPath, depth)));

        var children = component.GetChildren() as object[] ?? Array.Empty<object>();
        foreach (var child in children.OfType<IComponent2>())
        {
            string childName = child.Name2 ?? "Component";
            TraverseAssemblyComponent(child, $"{hierarchyPath}/{childName}", depth + 1, results);
        }
    }

    private static DiagnosticTargetContextInfo? CorrelateDiagnosticTargetContext(
        Feature feature,
        string name,
        string typeName,
        string? activeDocumentPath,
        IReadOnlyList<AssemblyComponentContext>? assemblyComponents)
    {
        if (assemblyComponents == null)
        {
            return null;
        }

        if (TryResolveExactComponentContextFromFeature(feature, assemblyComponents, out var exactFeatureMatch))
        {
            return CreateExactComponentContext(
                exactFeatureMatch.Info,
                assemblyComponents,
                activeDocumentPath,
                "Resolved via the feature's bound component.");
        }

        var exactMatches = FindExactDiagnosticComponentMatches(name, assemblyComponents);
        if (exactMatches.Count == 1)
        {
            return CreateExactComponentContext(
                exactMatches[0].Info,
                assemblyComponents,
                activeDocumentPath,
                "Resolved via an exact component-instance name match.");
        }

        if (exactMatches.Count > 1)
        {
            return TryCreateAmbiguousSharedSourceContext(
                exactMatches,
                activeDocumentPath,
                "Multiple component instances matched the diagnostic name.",
                unresolvedReason: "The diagnostic name matched multiple component instances that do not collapse to one exact target.");
        }

        var heuristicMatches = FindHeuristicDiagnosticComponentMatches(name, assemblyComponents);
        if (heuristicMatches.Count == 1)
        {
            return CreateExactComponentContext(
                heuristicMatches[0].Info,
                assemblyComponents,
                activeDocumentPath,
                "Resolved via source-name and component-name heuristics.");
        }

        if (heuristicMatches.Count > 1)
        {
            return TryCreateAmbiguousSharedSourceContext(
                heuristicMatches,
                activeDocumentPath,
                "Multiple reused component instances matched the diagnostic heuristics.",
                unresolvedReason: "The diagnostic heuristics matched multiple component instances without one exact target.");
        }

        if (IsClearlyAssemblyLevelDiagnostic(typeName))
        {
            return new DiagnosticTargetContextInfo(
                ScopeType: "assembly_level",
                IsExact: true,
                IsAmbiguous: false,
                DocumentPath: activeDocumentPath,
                HierarchyPath: null,
                ComponentName: null,
                SourceFilePath: null,
                OwningAssemblyHierarchyPath: null,
                OwningAssemblyFilePath: activeDocumentPath,
                SourceFileReuseCount: 0,
                Reason: $"The diagnostic type '{typeName}' is treated as assembly-level scope.",
                MatchingInstances: Array.Empty<ComponentInstanceInfo>());
        }

        return new DiagnosticTargetContextInfo(
            ScopeType: "unresolved_target_context",
            IsExact: false,
            IsAmbiguous: false,
            DocumentPath: activeDocumentPath,
            HierarchyPath: null,
            ComponentName: null,
            SourceFilePath: null,
            OwningAssemblyHierarchyPath: null,
            OwningAssemblyFilePath: activeDocumentPath,
            SourceFileReuseCount: 0,
            Reason: $"Could not correlate diagnostic '{name}' ({typeName}) to an exact component instance or a recognized assembly-level target.",
            MatchingInstances: Array.Empty<ComponentInstanceInfo>());
    }

    private static bool TryResolveExactComponentContextFromFeature(
        Feature feature,
        IReadOnlyList<AssemblyComponentContext> assemblyComponents,
        out AssemblyComponentContext match)
    {
        match = default!;

        object? specificFeature;
        try
        {
            specificFeature = feature.GetSpecificFeature2();
        }
        catch (COMException)
        {
            return false;
        }
        catch (TargetInvocationException)
        {
            return false;
        }

        if (specificFeature is not IComponent2 component)
        {
            return false;
        }

        var matches = assemblyComponents
            .Where(candidate => IsSameComponentInstance(candidate.Component, component))
            .ToList();
        if (matches.Count != 1)
        {
            return false;
        }

        match = matches[0];
        return true;
    }

    private static IReadOnlyList<AssemblyComponentContext> FindExactDiagnosticComponentMatches(
        string diagnosticName,
        IReadOnlyList<AssemblyComponentContext> assemblyComponents)
    {
        return assemblyComponents
            .Where(candidate =>
                string.Equals(candidate.Info.HierarchyPath, diagnosticName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.Info.Name, diagnosticName, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<AssemblyComponentContext> FindHeuristicDiagnosticComponentMatches(
        string diagnosticName,
        IReadOnlyList<AssemblyComponentContext> assemblyComponents)
    {
        var tokens = BuildDiagnosticLookupTokens(diagnosticName);
        if (tokens.Count == 0)
        {
            return Array.Empty<AssemblyComponentContext>();
        }

        return assemblyComponents
            .Where(candidate =>
            {
                string normalizedName = NormalizeLookupToken(candidate.Info.Name);
                string normalizedNameWithoutInstance = NormalizeLookupToken(TrimInstanceSuffix(candidate.Info.Name));
                string normalizedSourceStem = NormalizeLookupToken(Path.GetFileNameWithoutExtension(candidate.Info.Path));
                return tokens.Contains(normalizedName)
                    || tokens.Contains(normalizedNameWithoutInstance)
                    || tokens.Contains(normalizedSourceStem);
            })
            .ToList()
            .AsReadOnly();
    }

    private static HashSet<string> BuildDiagnosticLookupTokens(string? diagnosticName)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(diagnosticName))
        {
            return tokens;
        }

        string trimmed = diagnosticName.Trim();
        AddLookupToken(tokens, trimmed);
        AddLookupToken(tokens, TrimInstanceSuffix(trimmed));
        AddLookupToken(tokens, Path.GetFileNameWithoutExtension(trimmed));
        return tokens;
    }

    private static void AddLookupToken(ISet<string> tokens, string? value)
    {
        string normalized = NormalizeLookupToken(value);
        if (!string.IsNullOrEmpty(normalized))
        {
            tokens.Add(normalized);
        }
    }

    private static string NormalizeLookupToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (char current in value)
        {
            if (char.IsLetterOrDigit(current))
            {
                builder.Append(char.ToLowerInvariant(current));
            }
        }

        return builder.ToString();
    }

    private static string TrimInstanceSuffix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        int dashIndex = trimmed.LastIndexOf('-');
        if (dashIndex <= 0 || dashIndex + 1 >= trimmed.Length)
        {
            return trimmed;
        }

        for (int index = dashIndex + 1; index < trimmed.Length; index++)
        {
            if (!char.IsDigit(trimmed[index]))
            {
                return trimmed;
            }
        }

        return trimmed[..dashIndex];
    }

    private static DiagnosticTargetContextInfo CreateExactComponentContext(
        ComponentInstanceInfo match,
        IReadOnlyList<AssemblyComponentContext> assemblyComponents,
        string? activeDocumentPath,
        string reason)
    {
        string? owningAssemblyHierarchyPath = GetParentHierarchyPath(match.HierarchyPath);
        string? owningAssemblyFilePath = ResolveOwningAssemblyFilePath(
            assemblyComponents,
            owningAssemblyHierarchyPath,
            activeDocumentPath);

        return new DiagnosticTargetContextInfo(
            ScopeType: "component_instance",
            IsExact: true,
            IsAmbiguous: false,
            DocumentPath: activeDocumentPath,
            HierarchyPath: match.HierarchyPath,
            ComponentName: match.Name,
            SourceFilePath: NormalizePathOrNull(match.Path),
            OwningAssemblyHierarchyPath: owningAssemblyHierarchyPath,
            OwningAssemblyFilePath: owningAssemblyFilePath,
            SourceFileReuseCount: CountSourceFileReuse(assemblyComponents, match.Path),
            Reason: reason,
            MatchingInstances: new[] { match });
    }

    private static DiagnosticTargetContextInfo TryCreateAmbiguousSharedSourceContext(
        IReadOnlyList<AssemblyComponentContext> matches,
        string? activeDocumentPath,
        string ambiguousReason,
        string unresolvedReason)
    {
        var matchingInstances = matches
            .Select(match => match.Info)
            .DistinctBy(match => match.HierarchyPath, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();

        if (matchingInstances.Count == 0)
        {
            return new DiagnosticTargetContextInfo(
                ScopeType: "unresolved_target_context",
                IsExact: false,
                IsAmbiguous: false,
                DocumentPath: activeDocumentPath,
                HierarchyPath: null,
                ComponentName: null,
                SourceFilePath: null,
                OwningAssemblyHierarchyPath: null,
                OwningAssemblyFilePath: activeDocumentPath,
                SourceFileReuseCount: 0,
                Reason: unresolvedReason,
                MatchingInstances: Array.Empty<ComponentInstanceInfo>());
        }

        string? sharedSourcePath = NormalizePathOrNull(matchingInstances[0].Path);
        bool sameSourcePath = sharedSourcePath != null
            && matchingInstances.All(instance => PathsEqual(instance.Path, sharedSourcePath));

        if (sameSourcePath)
        {
            return new DiagnosticTargetContextInfo(
                ScopeType: "shared_source_scope",
                IsExact: false,
                IsAmbiguous: true,
                DocumentPath: activeDocumentPath,
                HierarchyPath: null,
                ComponentName: null,
                SourceFilePath: sharedSourcePath,
                OwningAssemblyHierarchyPath: null,
                OwningAssemblyFilePath: activeDocumentPath,
                SourceFileReuseCount: matchingInstances.Count,
                Reason: ambiguousReason,
                MatchingInstances: matchingInstances);
        }

        return new DiagnosticTargetContextInfo(
            ScopeType: "unresolved_target_context",
            IsExact: false,
            IsAmbiguous: false,
            DocumentPath: activeDocumentPath,
            HierarchyPath: null,
            ComponentName: null,
            SourceFilePath: null,
            OwningAssemblyHierarchyPath: null,
            OwningAssemblyFilePath: activeDocumentPath,
            SourceFileReuseCount: 0,
            Reason: unresolvedReason,
            MatchingInstances: matchingInstances);
    }

    private static bool IsClearlyAssemblyLevelDiagnostic(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        return typeName.Contains("Mate", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Folder", StringComparison.OrdinalIgnoreCase)
            || string.Equals(typeName, "MateGroup", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountSourceFileReuse(IReadOnlyList<AssemblyComponentContext> assemblyComponents, string? sourceFilePath)
    {
        string? normalizedPath = NormalizePathOrNull(sourceFilePath);
        if (normalizedPath == null)
        {
            return 0;
        }

        return assemblyComponents.Count(component => PathsEqual(component.Info.Path, normalizedPath));
    }

    private static string? ResolveOwningAssemblyFilePath(
        IReadOnlyList<AssemblyComponentContext> assemblyComponents,
        string? owningAssemblyHierarchyPath,
        string? activeAssemblyPath)
    {
        if (owningAssemblyHierarchyPath == null)
        {
            return activeAssemblyPath;
        }

        string? candidateHierarchyPath = owningAssemblyHierarchyPath;
        while (candidateHierarchyPath != null)
        {
            string? candidatePath = assemblyComponents
                .FirstOrDefault(component => string.Equals(component.Info.HierarchyPath, candidateHierarchyPath, StringComparison.OrdinalIgnoreCase))
                ?.Info.Path;
            if (!string.IsNullOrWhiteSpace(candidatePath))
            {
                return NormalizePathOrNull(candidatePath);
            }

            candidateHierarchyPath = GetParentHierarchyPath(candidateHierarchyPath);
        }

        return activeAssemblyPath;
    }

    private static string? GetParentHierarchyPath(string? hierarchyPath)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPath))
        {
            return null;
        }

        int separatorIndex = hierarchyPath.LastIndexOf('/');
        return separatorIndex <= 0 ? null : hierarchyPath[..separatorIndex];
    }

    private static bool IsSameComponentInstance(IComponent2 left, IComponent2 right)
    {
        return string.Equals(left.Name2, right.Name2, StringComparison.OrdinalIgnoreCase)
            && PathsEqual(left.GetPathName(), right.GetPathName());
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

    private static IEnumerable<string> ExpandSelectionTypes(string selType)
    {
        if (SelectionTypeAliases.TryGetValue(selType, out var aliases))
        {
            return aliases;
        }

        return [selType];
    }

    private static FeatureErrorInfo GetFeatureErrorInfo(Feature feature)
    {
        bool isWarning;
        int errorCode = feature.GetErrorCode2(out isWarning);
        return new FeatureErrorInfo(errorCode, isWarning, SolidWorksApiErrorFactory.CreateFeatureErrorCodeInfo(errorCode));
    }

    private static string BuildFeatureKey(string name, string typeName)
        => string.Concat(name, "||", typeName);

    private static int[] ToIntArray(object? raw)
    {
        return raw switch
        {
            null => Array.Empty<int>(),
            int[] ints => ints,
            object[] objects => objects.Select(ToInt).ToArray(),
            _ => Array.Empty<int>(),
        };
    }

    private static int ToInt(object? value)
    {
        return value switch
        {
            null => 0,
            int intValue => intValue,
            short shortValue => shortValue,
            long longValue => unchecked((int)longValue),
            _ => Convert.ToInt32(value),
        };
    }

    private static bool[] ToBoolArray(object? raw)
    {
        return raw switch
        {
            null => Array.Empty<bool>(),
            bool[] bools => bools,
            object[] objects => objects.Select(ToBool).ToArray(),
            _ => Array.Empty<bool>(),
        };
    }

    private static bool ToBool(object? value)
    {
        return value switch
        {
            null => false,
            bool boolValue => boolValue,
            short shortValue => shortValue != 0,
            int intValue => intValue != 0,
            _ => Convert.ToBoolean(value),
        };
    }

    private static bool IsPlaneSelection(string selType)
        => ExpandSelectionTypes(selType)
            .Any(type => string.Equals(type, "PLANE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "swSelDATUMPLANES", StringComparison.OrdinalIgnoreCase));

    private static SelectionResult? TrySelectLocalizedStandardPlane(IModelDoc2 doc, string requestedName, string requestedType)
    {
        var requestedKind = GetStandardPlaneKind(requestedName);
        if (requestedKind == StandardPlaneKind.Unknown)
        {
            return null;
        }

        var planes = EnumerateReferencePlanes(doc).ToList();
        var plane = planes.FirstOrDefault(candidate =>
        {
            var planeKind = GetStandardPlaneKind(candidate.Name);
            if (planeKind == StandardPlaneKind.Unknown)
            {
                planeKind = GetStandardPlaneKind(candidate.SelectionName);
            }

            return planeKind == requestedKind;
        });

        if (plane == null)
        {
            return null;
        }

        var candidateTypes = ExpandSelectionTypes(requestedType)
            .Concat(ExpandSelectionTypes(plane.SelectionType))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidateType in candidateTypes)
        {
            bool ok = doc.SelectByID(plane.SelectionName, candidateType, 0, 0, 0);
            if (ok)
            {
                return new SelectionResult(
                    true,
                    $"Selected '{plane.SelectionName}' via localized fallback for '{requestedName}' (type '{candidateType}')");
            }
        }

        return null;
    }

    private static StandardPlaneKind GetStandardPlaneKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return StandardPlaneKind.Unknown;
        }

        var text = value.Trim().ToLowerInvariant();

        if (text.Contains("front") || text.Contains("前视") || text.Contains("前基准") || text.Contains("前平面"))
        {
            return StandardPlaneKind.Front;
        }

        if (text.Contains("top") || text.Contains("上视") || text.Contains("上基准") || text.Contains("上平面"))
        {
            return StandardPlaneKind.Top;
        }

        if (text.Contains("right") || text.Contains("右视") || text.Contains("右基准") || text.Contains("右平面"))
        {
            return StandardPlaneKind.Right;
        }

        return StandardPlaneKind.Unknown;
    }

    private IEnumerable<(IBody2 Body, string? ComponentName)> EnumerateBodyContexts()
    {
        var doc = GetActiveModelDoc();

        if (doc is IPartDoc part)
        {
            foreach (var body in GetBodies(part))
            {
                yield return (body, null);
            }

            yield break;
        }

        if (doc is IAssemblyDoc assembly)
        {
            var components = (object[]?)assembly.GetComponents(true) ?? Array.Empty<object>();
            foreach (var component in EnumerateAssemblyComponentsRecursive(components.OfType<IComponent2>()))
            {
                foreach (var body in GetBodies(component))
                {
                    yield return (body, component.Name2);
                }
            }

            yield break;
        }

        throw new InvalidOperationException("Topology listing is only supported for part and assembly documents.");
    }

    private static IEnumerable<EntityCandidate> EnumerateEntitiesForBody(IBody2 body, string? componentName)
    {
        foreach (var face in ((object[]?)body.GetFaces() ?? Array.Empty<object>()).OfType<IFace2>())
        {
            yield return new EntityCandidate(-1, (IEntity)face, SelectableEntityType.Face, componentName, GetBox(face));
        }

        foreach (var edge in ((object[]?)body.GetEdges() ?? Array.Empty<object>()).OfType<IEdge>())
        {
            yield return new EntityCandidate(-1, (IEntity)edge, SelectableEntityType.Edge, componentName, GetBox(edge));
        }

        foreach (var vertex in ((object[]?)body.GetVertices() ?? Array.Empty<object>()).OfType<IVertex>())
        {
            yield return new EntityCandidate(-1, (IEntity)vertex, SelectableEntityType.Vertex, componentName, GetBox(vertex));
        }
    }

    private static IEnumerable<IComponent2> EnumerateAssemblyComponentsRecursive(IEnumerable<IComponent2> components)
    {
        foreach (var component in components)
        {
            yield return component;

            var children = (object[]?)component.GetChildren() ?? Array.Empty<object>();
            foreach (var child in EnumerateAssemblyComponentsRecursive(children.OfType<IComponent2>()))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<IBody2> GetBodies(IPartDoc part)
    {
        return ((object[]?)part.GetBodies2((int)swBodyType_e.swSolidBody, true) ?? Array.Empty<object>())
            .OfType<IBody2>();
    }

    private static IEnumerable<IBody2> GetBodies(IComponent2 component)
    {
        return (component.GetBodies3((int)swBodyType_e.swSolidBody, out _) as object[] ?? Array.Empty<object>())
            .OfType<IBody2>();
    }

    private static double[]? GetBox(IFace2 face)
        => ToDoubleArray(face.GetBox());

    private static double[]? GetBox(IEdge edge)
    {
        var start = ToDoubleArray((edge.GetStartVertex() as IVertex)?.GetPoint());
        var end = ToDoubleArray((edge.GetEndVertex() as IVertex)?.GetPoint());

        if (start == null && end == null)
        {
            return null;
        }

        var first = start ?? end!;
        var second = end ?? first;

        if (first.Length < 3 || second.Length < 3)
        {
            return first;
        }

        return
        [
            Math.Min(first[0], second[0]),
            Math.Min(first[1], second[1]),
            Math.Min(first[2], second[2]),
            Math.Max(first[0], second[0]),
            Math.Max(first[1], second[1]),
            Math.Max(first[2], second[2]),
        ];
    }

    private static double[]? GetBox(IVertex vertex)
    {
        var point = ToDoubleArray(vertex.GetPoint());
        if (point == null || point.Length < 3)
        {
            return point;
        }

        return [point[0], point[1], point[2], point[0], point[1], point[2]];
    }

    private static double[]? ToDoubleArray(object? raw)
    {
        return raw switch
        {
            null => null,
            double[] doubles => doubles,
            object[] objects => objects.OfType<double>().ToArray(),
            _ => null,
        };
    }

    private static double? NormalizeMeasureValue(double value)
        => value == -1 ? null : value;

    private static bool IsSketchLike(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        return string.Equals(typeName, "ProfileFeature", StringComparison.OrdinalIgnoreCase)
            || string.Equals(typeName, "3DProfileFeature", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasChildFeatures(Feature feature)
    {
        try
        {
            return (feature.GetChildren() as object[] ?? Array.Empty<object>()).Length > 0;
        }
        catch (COMException)
        {
            return false;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
    }

    private static bool TryDeleteFeature(IModelDoc2 doc, Feature feature)
    {
        try
        {
            doc.ClearSelection2(true);
            if (!feature.Select2(false, -1))
            {
                return false;
            }

            bool deleted = doc.Extension.DeleteSelection2(0);
            doc.ClearSelection2(true);
            return deleted;
        }
        catch (COMException)
        {
            doc.ClearSelection2(true);
            return false;
        }
        catch (TargetInvocationException)
        {
            doc.ClearSelection2(true);
            return false;
        }
    }

    private static EditStateInfo GetEditState(IModelDoc2 doc)
    {
        if (doc.GetActiveSketch2() != null)
        {
            return new EditStateInfo(
                IsEditing: true,
                EditMode: "sketch",
                CanReadFeatureTree: false,
                CanDeleteFeatures: false);
        }

        return new EditStateInfo(
            IsEditing: false,
            EditMode: "none",
            CanReadFeatureTree: true,
            CanDeleteFeatures: true);
    }

    private static void EnsureNotEditing(IModelDoc2 doc, string operation)
    {
        var state = GetEditState(doc);
        if (!state.IsEditing)
        {
            return;
        }

        throw new InvalidOperationException($"Finish the active {state.EditMode} before {operation}.");
    }

    private static SelectData CreateSelectData(ISelectionMgr selectionManager, int mark)
    {
        var selectData = selectionManager.CreateSelectData()
            ?? throw new InvalidOperationException("Could not create selection data.");

        var markProperty = selectData.GetType().GetProperty("Mark", BindingFlags.Instance | BindingFlags.Public);
        if (markProperty?.CanWrite == true)
        {
            markProperty.SetValue(selectData, mark);
        }

        return selectData;
    }

    private static string? SafeGetCurrentLanguage(ISldWorksApp swApp)
    {
        try
        {
            return swApp.GetCurrentLanguage();
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? SafeGetFeatureTypeName(Feature feature)
    {
        try
        {
            return feature.GetTypeName2();
        }
        catch (COMException)
        {
            return null;
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }

    private static string? SafeGetFeatureName(Feature feature)
    {
        try
        {
            return feature.Name;
        }
        catch (COMException)
        {
            return null;
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }

    private static (string? SelectionName, string? SelectionType) SafeGetSelectionIdentity(Feature feature)
    {
        try
        {
            string selectionType;
            string selectionName = feature.GetNameForSelection(out selectionType);
            return (selectionName, selectionType);
        }
        catch (COMException)
        {
            return (null, null);
        }
        catch (TargetInvocationException)
        {
            return (null, null);
        }
    }
}
