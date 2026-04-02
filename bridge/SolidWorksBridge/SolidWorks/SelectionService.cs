using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SolidWorksBridge.SolidWorks;

/// <summary>
/// Result of a selection operation.
/// </summary>
public record SelectionResult(bool Success, string Message);

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

    private sealed record FeatureNode(
        Feature Feature,
        int Index,
        string Name,
        string TypeName,
        bool IsSketch,
        bool HasChildren);

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
            ? new SelectionResult(true, $"Selected {entityType} at index {index}")
            : new SelectionResult(false, $"Failed to select {entityType} at index {index}");
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

    private static IEnumerable<string> ExpandSelectionTypes(string selType)
    {
        if (SelectionTypeAliases.TryGetValue(selType, out var aliases))
        {
            return aliases;
        }

        return [selType];
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
