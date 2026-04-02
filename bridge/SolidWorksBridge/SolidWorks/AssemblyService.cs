using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksBridge.SolidWorks;

/// <summary>
/// Info about a component in an assembly.
/// </summary>
public record ComponentInfo(string Name, string Path);

/// <summary>
/// Info about a component instance discovered by recursively traversing an assembly tree.
/// </summary>
public record ComponentInstanceInfo(string Name, string Path, string HierarchyPath, int Depth);

/// <summary>
/// Summary of a static interference check in an assembly.
/// </summary>
public record AssemblyInterferenceCheckResult(
    bool HasInterference,
    bool TreatCoincidenceAsInterference,
    int CheckedComponentCount,
    int InterferingFaceCount,
    IReadOnlyList<ComponentInstanceInfo> InterferingComponents);

public record AssemblyComponentReplacementResult(
    string ReplacedHierarchyPath,
    string ReplacementFilePath,
    string ConfigName,
    bool ReplaceAllInstances,
    int UseConfigChoice,
    bool ReattachMates,
    bool Success);

public record MateOperationResult(string MateType, int ErrorStatus, string ErrorName, string ErrorDescription);

/// <summary>
/// Mate type for assembly constraints.
/// Values match swMateType_e.
/// </summary>
public enum MateType
{
    Coincident = 0,
    Concentric = 1,
    Perpendicular = 2,
    Parallel = 3,
    Distance = 5,
    Angle = 6,
}

/// <summary>
/// Mate alignment for assembly constraints.
/// Values match swMateAlign_e.
/// </summary>
public enum MateAlign
{
    None = 0,
    AntiAligned = 1,
    Closest = 2,
}

/// <summary>
/// Operations on SolidWorks assembly documents.
/// </summary>
public interface IAssemblyService
{
    /// <summary>
    /// Insert a component at the given position (meters). Returns component info.
    /// </summary>
    ComponentInfo InsertComponent(string filePath, double x = 0, double y = 0, double z = 0);

    /// <summary>
    /// Add a Coincident mate between the two currently-selected entities.
    /// </summary>
    MateOperationResult AddMateCoincident(MateAlign align = MateAlign.Closest);

    /// <summary>
    /// Add a Concentric mate between the two currently-selected entities.
    /// </summary>
    MateOperationResult AddMateConcentric(MateAlign align = MateAlign.Closest);

    /// <summary>
    /// Add a Parallel mate between the two currently-selected entities.
    /// </summary>
    MateOperationResult AddMateParallel(MateAlign align = MateAlign.Closest);

    /// <summary>
    /// Add a Distance mate between the two currently-selected entities.
    /// </summary>
    MateOperationResult AddMateDistance(double distance, MateAlign align = MateAlign.Closest);

    /// <summary>
    /// Add an Angle mate between the two currently-selected entities.
    /// </summary>
    MateOperationResult AddMateAngle(double angleDegrees, MateAlign align = MateAlign.Closest);

    /// <summary>
    /// List all top-level components in the active assembly.
    /// </summary>
    IReadOnlyList<ComponentInfo> ListComponents();

    /// <summary>
    /// List all component instances in the active assembly by recursively traversing subassemblies.
    /// </summary>
    IReadOnlyList<ComponentInstanceInfo> ListComponentsRecursive();

    /// <summary>
    /// Run a static interference check for the active assembly or a subset of component instances.
    /// This follows the official ToolsCheckInterference2 workflow and temporarily changes the SolidWorks selection set.
    /// </summary>
    AssemblyInterferenceCheckResult CheckInterference(
        IReadOnlyList<string>? hierarchyPaths = null,
        bool treatCoincidenceAsInterference = false);

    /// <summary>
    /// Replace a top-level component instance in the active assembly with another model file.
    /// </summary>
    AssemblyComponentReplacementResult ReplaceComponent(
        string hierarchyPath,
        string replacementFilePath,
        string configName = "",
        bool replaceAllInstances = false,
        int useConfigChoice = (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_MatchName,
        bool reattachMates = true);

}

/// <summary>
/// Implements <see cref="IAssemblyService"/> via SolidWorks IAssemblyDoc COM API.
/// </summary>
public class AssemblyService : IAssemblyService
{
    private readonly ISwConnectionManager _cm;

    public AssemblyService(ISwConnectionManager cm)
    {
        _cm = cm ?? throw new ArgumentNullException(nameof(cm));
    }

    public ComponentInfo InsertComponent(string filePath, double x = 0, double y = 0, double z = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath must not be empty", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Component file was not found: {filePath}", filePath);

        _cm.EnsureConnected();
        var assy = GetAssemblyDoc();

        // For a saved standalone part, let SolidWorks resolve the model's active/default
        // configuration instead of forcing an existing selected config in the target assembly.
        var comp = assy.AddComponent5(filePath, 0, "", false, "", x, y, z) as IComponent2
            ?? throw new InvalidOperationException($"Failed to insert component: {filePath}");

        return new ComponentInfo(comp.Name2, comp.GetPathName());
    }

    public MateOperationResult AddMateCoincident(MateAlign align = MateAlign.Closest)
        => AddMate(MateType.Coincident, align);

    public MateOperationResult AddMateConcentric(MateAlign align = MateAlign.Closest)
        => AddMate(MateType.Concentric, align);

    public MateOperationResult AddMateParallel(MateAlign align = MateAlign.Closest)
        => AddMate(MateType.Parallel, align);

    public MateOperationResult AddMateDistance(double distance, MateAlign align = MateAlign.Closest)
        => AddMate(MateType.Distance, align, distance: distance);

    public MateOperationResult AddMateAngle(double angleDegrees, MateAlign align = MateAlign.Closest)
        => AddMate(MateType.Angle, align, angle: angleDegrees * Math.PI / 180.0);

    public IReadOnlyList<ComponentInfo> ListComponents()
    {
        _cm.EnsureConnected();
        var assy = GetAssemblyDoc();

        var raw = assy.GetComponents(true) as object[]
            ?? [];

        return raw
            .OfType<IComponent2>()
            .Select(c => new ComponentInfo(c.Name2, c.GetPathName()))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ComponentInstanceInfo> ListComponentsRecursive()
    {
        _cm.EnsureConnected();
        var assy = GetAssemblyDoc();

        var instances = EnumerateComponentInstances(assy)
            .Select(instance => instance.Info)
            .ToList();

        return instances.AsReadOnly();
    }

    public AssemblyInterferenceCheckResult CheckInterference(
        IReadOnlyList<string>? hierarchyPaths = null,
        bool treatCoincidenceAsInterference = false)
    {
        _cm.EnsureConnected();
        var assy = GetAssemblyDoc();

        var instances = EnumerateComponentInstances(assy);
        var requestedPaths = hierarchyPaths == null || hierarchyPaths.Count == 0
            ? null
            : new HashSet<string>(hierarchyPaths, StringComparer.OrdinalIgnoreCase);

        InterferenceDetectionMgr? detectionManager = null;
        try
        {
            assy.ToolsCheckInterference();
            detectionManager = assy.InterferenceDetectionManager
                ?? throw new InvalidOperationException("SolidWorks did not provide an interference detection manager.");

            detectionManager.TreatCoincidenceAsInterference = treatCoincidenceAsInterference;
            detectionManager.TreatSubAssembliesAsComponents = false;
            detectionManager.IncludeMultibodyPartInterferences = true;
            detectionManager.MakeInterferingPartsTransparent = true;
            detectionManager.CreateFastenersFolder = false;
            detectionManager.IgnoreHiddenBodies = false;
            detectionManager.ShowIgnoredInterferences = false;
            detectionManager.UseTransform = false;
            detectionManager.NonInterferingComponentDisplay = (int)swNonInterferingComponentDisplay_e.swNonInterferingComponentDisplay_Wireframe;

            var interferences = (detectionManager.GetInterferences() as object[] ?? Array.Empty<object>())
                .OfType<IInterference>()
                .ToList();

            var matchedInfos = new List<ComponentInstanceInfo>();
            int interferingFaceCount = 0;
            foreach (var interference in interferences)
            {
                var components = (interference.Components as object[] ?? Array.Empty<object>())
                    .OfType<IComponent2>()
                    .ToList();
                var componentInfos = instances
                    .Where(instance => components.Any(component => IsSameComponentInstance(instance.Component, component)))
                    .Select(instance => instance.Info)
                    .DistinctBy(info => info.HierarchyPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (requestedPaths != null && !componentInfos.Any(info => requestedPaths.Contains(info.HierarchyPath)))
                {
                    continue;
                }

                matchedInfos.AddRange(componentInfos);
                if (!interference.IsPossibleInterference)
                {
                    interferingFaceCount += Math.Max(interference.GetComponentCount(), 0);
                }
            }

            var interferingInfos = matchedInfos
                .DistinctBy(info => info.HierarchyPath, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();

            int checkedComponentCount = requestedPaths == null
                ? instances.Count
                : instances.Count(instance => requestedPaths.Contains(instance.Info.HierarchyPath));

            return new AssemblyInterferenceCheckResult(
                HasInterference: interferingInfos.Count > 0,
                TreatCoincidenceAsInterference: treatCoincidenceAsInterference,
                CheckedComponentCount: checkedComponentCount,
                InterferingFaceCount: interferingFaceCount,
                InterferingComponents: interferingInfos);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            throw SolidWorksApiErrorFactory.FromComException(
                "IInterferenceDetectionMgr.GetInterferences",
                ex,
                new Dictionary<string, object?>
                {
                    ["requestedHierarchyPaths"] = hierarchyPaths == null ? null : string.Join(" | ", hierarchyPaths),
                    ["treatCoincidenceAsInterference"] = treatCoincidenceAsInterference,
                });
        }
        finally
        {
            detectionManager?.Done();
        }
    }

    public AssemblyComponentReplacementResult ReplaceComponent(
        string hierarchyPath,
        string replacementFilePath,
        string configName = "",
        bool replaceAllInstances = false,
        int useConfigChoice = (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_MatchName,
        bool reattachMates = true)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPath))
            throw new ArgumentException("hierarchyPath must not be empty", nameof(hierarchyPath));
        if (string.IsNullOrWhiteSpace(replacementFilePath))
            throw new ArgumentException("replacementFilePath must not be empty", nameof(replacementFilePath));

        _cm.EnsureConnected();
        var assy = GetAssemblyDoc();
        var doc = (IModelDoc2)assy;

        var target = EnumerateComponentInstances(assy)
            .FirstOrDefault(instance => string.Equals(instance.Info.HierarchyPath, hierarchyPath, StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                "IAssemblyDoc.ReplaceComponents2",
                "The requested component instance was not found in the active assembly.",
                new Dictionary<string, object?>
                {
                    ["hierarchyPath"] = hierarchyPath,
                    ["replacementFilePath"] = replacementFilePath,
                });
        }

        if (target.Info.Depth != 0)
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                "IAssemblyDoc.ReplaceComponents2",
                "Only top-level components in the active assembly can be replaced. Open the owning subassembly and retry.",
                new Dictionary<string, object?>
                {
                    ["hierarchyPath"] = hierarchyPath,
                    ["depth"] = target.Info.Depth,
                });
        }

        var selectionManager = doc.ISelectionManager
            ?? throw new InvalidOperationException("Selection manager is not available for the active assembly.");

        doc.ClearSelection2(true);
        try
        {
            var selectData = (SelectData)selectionManager.CreateSelectData();
            if (!target.Component.Select4(false, selectData, false))
            {
                throw SolidWorksApiErrorFactory.FromValidationFailure(
                    "IComponent2.Select4",
                    $"Failed to select component '{hierarchyPath}' for replacement.",
                    new Dictionary<string, object?>
                    {
                        ["hierarchyPath"] = hierarchyPath,
                        ["replacementFilePath"] = replacementFilePath,
                    });
            }

            bool success;
            try
            {
                success = assy.ReplaceComponents2(replacementFilePath, configName, replaceAllInstances, useConfigChoice, reattachMates);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                throw SolidWorksApiErrorFactory.FromComException(
                    "IAssemblyDoc.ReplaceComponents2",
                    ex,
                    new Dictionary<string, object?>
                    {
                        ["hierarchyPath"] = hierarchyPath,
                        ["replacementFilePath"] = replacementFilePath,
                        ["configName"] = configName,
                        ["replaceAllInstances"] = replaceAllInstances,
                        ["useConfigChoice"] = useConfigChoice,
                        ["reattachMates"] = reattachMates,
                    });
            }

            if (!success)
            {
                throw SolidWorksApiErrorFactory.FromValidationFailure(
                    "IAssemblyDoc.ReplaceComponents2",
                    "SolidWorks did not replace the selected component.",
                    new Dictionary<string, object?>
                    {
                        ["hierarchyPath"] = hierarchyPath,
                        ["replacementFilePath"] = replacementFilePath,
                        ["configName"] = configName,
                        ["replaceAllInstances"] = replaceAllInstances,
                        ["useConfigChoice"] = useConfigChoice,
                        ["reattachMates"] = reattachMates,
                    });
            }

            return new AssemblyComponentReplacementResult(
                hierarchyPath,
                replacementFilePath,
                configName,
                replaceAllInstances,
                useConfigChoice,
                reattachMates,
                success);
        }
        finally
        {
            doc.ClearSelection2(true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private sealed record ComponentInstance(IComponent2 Component, ComponentInstanceInfo Info);

    private static IReadOnlyList<ComponentInstance> EnumerateComponentInstances(IAssemblyDoc assy)
    {
        var raw = assy.GetComponents(true) as object[]
            ?? [];

        var results = new List<ComponentInstance>();
        foreach (var component in raw.OfType<IComponent2>())
        {
            TraverseComponent(component, component.Name2 ?? "Component", depth: 0, results);
        }

        return results.AsReadOnly();
    }

    private static IReadOnlyList<ComponentInstance> SelectInstances(
        IReadOnlyList<ComponentInstance> instances,
        IReadOnlyList<string>? hierarchyPaths)
    {
        if (hierarchyPaths == null || hierarchyPaths.Count == 0)
        {
            return instances;
        }

        var requested = new HashSet<string>(hierarchyPaths, StringComparer.OrdinalIgnoreCase);
        return instances
            .Where(instance => requested.Contains(instance.Info.HierarchyPath))
            .ToList()
            .AsReadOnly();
    }

    private static void TraverseComponent(
        IComponent2 component,
        string hierarchyPath,
        int depth,
        ICollection<ComponentInstance> results)
    {
        var name = component.Name2 ?? $"Component{depth}";
        string path = component.GetPathName() ?? string.Empty;

        results.Add(new ComponentInstance(component, new ComponentInstanceInfo(name, path, hierarchyPath, depth)));

        var children = component.GetChildren() as object[]
            ?? [];

        foreach (var child in children.OfType<IComponent2>())
        {
            var childName = child.Name2 ?? "Component";
            TraverseComponent(child, $"{hierarchyPath}/{childName}", depth + 1, results);
        }
    }

    private static bool IsSameComponentInstance(IComponent2 left, IComponent2 right)
    {
        return string.Equals(left.Name2, right.Name2, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.GetPathName(), right.GetPathName(), StringComparison.OrdinalIgnoreCase);
    }

    private IAssemblyDoc GetAssemblyDoc()
    {
        var doc = _cm.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException("No active document");

        return doc as IAssemblyDoc
            ?? throw new InvalidOperationException("Active document is not an assembly");
    }

    private MateOperationResult AddMate(MateType type, MateAlign align,
        double distance = 0, double angle = 0)
    {
        _cm.EnsureConnected();
        var assy = GetAssemblyDoc();

        int errors = 0;
        // AddMate5(mateType, align, flip, distance, distMax, distMin,
        //          gearNumer, gearDenom, angle, angleMax, angleMin,
        //          forPosOnly, lockRot, widthOpt, ref errors)
        var mate = assy.AddMate5(
            (int)type, (int)align, false,
            distance, 0, 0,
            0, 0,
            angle, 0, 0,
            false, false, 0,
            out errors);

        var mateError = (swAddMateError_e)errors;
        if (mate == null || mateError != swAddMateError_e.swAddMateError_NoError)
        {
            throw SolidWorksApiErrorFactory.FromMateFailure(
                "IAssemblyDoc.AddMate5",
                $"Failed to create {type} mate.",
                errors,
                new Dictionary<string, object?>
                {
                    ["mateType"] = type,
                    ["align"] = align,
                    ["distance"] = distance,
                    ["angleRadians"] = angle,
                });
        }

        var codeInfo = SolidWorksApiErrorFactory.DecodeValue<swAddMateError_e>(errors);
        return new MateOperationResult(type.ToString(), errors, codeInfo.Name, codeInfo.Description);
    }
}
