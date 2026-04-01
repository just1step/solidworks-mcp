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
    /// Select a topology entity by the index returned from <see cref="ListEntities"/>.
    /// </summary>
    SelectionResult SelectEntity(
        SelectableEntityType entityType,
        int index,
        bool append = false,
        int mark = 0,
        string? componentName = null);

    /// <summary>Clear the current selection set.</summary>
    void ClearSelection();
}

/// <summary>
/// Implements <see cref="ISelectionService"/> via <see cref="ISwConnectionManager"/>.
/// </summary>
public class SelectionService : ISelectionService
{
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

    private static IEnumerable<string> ExpandSelectionTypes(string selType)
    {
        if (SelectionTypeAliases.TryGetValue(selType, out var aliases))
        {
            return aliases;
        }

        return [selType];
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
            foreach (var component in components.OfType<IComponent2>())
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
