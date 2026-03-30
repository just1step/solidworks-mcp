using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksBridge.SolidWorks;

/// <summary>
/// Info about a component in an assembly.
/// </summary>
public record ComponentInfo(string Name, string Path);

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
    void AddMateCoincident(MateAlign align = MateAlign.Closest);

    /// <summary>
    /// Add a Concentric mate between the two currently-selected entities.
    /// </summary>
    void AddMateConcentric(MateAlign align = MateAlign.Closest);

    /// <summary>
    /// Add a Parallel mate between the two currently-selected entities.
    /// </summary>
    void AddMateParallel(MateAlign align = MateAlign.Closest);

    /// <summary>
    /// Add a Distance mate between the two currently-selected entities.
    /// </summary>
    void AddMateDistance(double distance, MateAlign align = MateAlign.Closest);

    /// <summary>
    /// Add an Angle mate between the two currently-selected entities.
    /// </summary>
    void AddMateAngle(double angleDegrees, MateAlign align = MateAlign.Closest);

    /// <summary>
    /// List all top-level components in the active assembly.
    /// </summary>
    IReadOnlyList<ComponentInfo> ListComponents();
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

        _cm.EnsureConnected();
        var assy = GetAssemblyDoc();

        // AddComponent5(fileName, configOption, newConfigName, useExistingConfig, existingConfigName, x, y, z)
        // configOption 0 = swAddComponentConfigOptions_CurrentSelectedConfig
        var comp = assy.AddComponent5(filePath, 0, "", true, "", x, y, z) as IComponent2
            ?? throw new InvalidOperationException($"Failed to insert component: {filePath}");

        return new ComponentInfo(comp.Name2, comp.GetPathName());
    }

    public void AddMateCoincident(MateAlign align = MateAlign.Closest)
        => AddMate(MateType.Coincident, align);

    public void AddMateConcentric(MateAlign align = MateAlign.Closest)
        => AddMate(MateType.Concentric, align);

    public void AddMateParallel(MateAlign align = MateAlign.Closest)
        => AddMate(MateType.Parallel, align);

    public void AddMateDistance(double distance, MateAlign align = MateAlign.Closest)
        => AddMate(MateType.Distance, align, distance: distance);

    public void AddMateAngle(double angleDegrees, MateAlign align = MateAlign.Closest)
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

    // ── Helpers ───────────────────────────────────────────────────

    private IAssemblyDoc GetAssemblyDoc()
    {
        var doc = _cm.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException("No active document");

        return doc as IAssemblyDoc
            ?? throw new InvalidOperationException("Active document is not an assembly");
    }

    private void AddMate(MateType type, MateAlign align,
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

        if (mate == null || errors != 0)
            throw new InvalidOperationException(
                $"AddMate5 failed (type={type}, errors={errors}) — ensure two compatible entities are selected");
    }
}
