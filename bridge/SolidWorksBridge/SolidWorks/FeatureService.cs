using SolidWorks.Interop.sldworks;

namespace SolidWorksBridge.SolidWorks;

/// <summary>
/// Info about a feature that was created.
/// </summary>
public record FeatureInfo(string Name, string Type);

/// <summary>
/// End condition types for extrude/cut operations.
/// Values match swEndConditions_e: 0=Blind, 1=ThroughAll, 6=MidPlane.
/// </summary>
public enum EndCondition
{
    Blind = 0,
    ThroughAll = 1,
    MidPlane = 6,
}

/// <summary>
/// Parametric feature operations on the active part document.
/// The appropriate sketch or edges must be selected before calling these.
/// </summary>
public interface IFeatureService
{
    /// <summary>
    /// Extrude the selected sketch as a boss (add material).
    /// </summary>
    FeatureInfo Extrude(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false);

    /// <summary>
    /// Extrude the selected sketch as a cut (remove material).
    /// </summary>
    FeatureInfo ExtrudeCut(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false);

    /// <summary>
    /// Revolve the selected sketch around the selected axis line.
    /// </summary>
    FeatureInfo Revolve(double angleDegrees, bool isCut = false);

    /// <summary>
    /// Fillet selected edges with the given radius (meters).
    /// </summary>
    FeatureInfo Fillet(double radius);

    /// <summary>
    /// Chamfer selected edges: symmetric distance (meters).
    /// </summary>
    FeatureInfo Chamfer(double distance);

    /// <summary>
    /// Shell the active solid: open the selected faces, leave given wall thickness (meters).
    /// </summary>
    FeatureInfo Shell(double thickness);

    /// <summary>
    /// Create a simple cylindrical hole on the selected face at the selected point.
    /// </summary>
    FeatureInfo SimpleHole(double diameter, double depth, EndCondition endCondition = EndCondition.Blind);
}

/// <summary>
/// Implements <see cref="IFeatureService"/> via SolidWorks FeatureManager COM API.
/// </summary>
public class FeatureService : IFeatureService
{
    private readonly ISwConnectionManager _cm;

    public FeatureService(ISwConnectionManager cm)
    {
        _cm = cm ?? throw new ArgumentNullException(nameof(cm));
    }

    public FeatureInfo Extrude(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false)
    {
        _cm.EnsureConnected();
        var doc = GetModelDoc();

        // FeatureBoss2: called while sketch is in edit mode; void return.
        // Retrieve the created feature with IFeatureByPositionReverse(0).
        doc.FeatureBoss2(
            Sd: true, Flip: flipDirection, Dir: !flipDirection,
            T1: (int)endCondition, T2: 0,
            D1: depth, D2: 0,
            Dchk1: false, Dchk2: false,
            Ddir1: false, Ddir2: false,
            Dang1: 0, Dang2: 0,
            OffsetReverse1: false, OffsetReverse2: false,
            TranslateSurface1: false, TranslateSurface2: false);

        var feature = doc.IFeatureByPositionReverse(0)
            ?? throw new InvalidOperationException(
                "Extrude failed — ensure a sketch is in edit mode on the active document");

        return new FeatureInfo(feature.Name, "Extrude");
    }

    public FeatureInfo ExtrudeCut(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false)
    {
        _cm.EnsureConnected();
        var doc = GetModelDoc();

        // FeatureCut2: called while sketch is in edit mode; void return.
        doc.FeatureCut2(
            Sd: true, Flip: flipDirection, Dir: !flipDirection,
            T1: (int)endCondition, T2: 0,
            D1: depth, D2: 0,
            Dchk1: false, Dchk2: false,
            Ddir1: false, Ddir2: false,
            Dang1: 0, Dang2: 0,
            OffsetReverse1: false, OffsetReverse2: false,
            KeepPieceIndex: 0);

        var feature = doc.IFeatureByPositionReverse(0)
            ?? throw new InvalidOperationException(
                "ExtrudeCut failed — ensure a sketch is in edit mode on the active document");

        return new FeatureInfo(feature.Name, "ExtrudeCut");
    }

    public FeatureInfo Revolve(double angleDegrees, bool isCut = false)
    {
        _cm.EnsureConnected();
        var fm = GetFeatureManager();
        double angleRad = angleDegrees * Math.PI / 180.0;

        // FeatureRevolve2: SingleDir, IsSolid, IsThin, IsCut, ReverseDir,
        //                  BothDirSameEntity, Dir1Type=0(blind), Dir2Type=0,
        //                  Dir1Angle, Dir2Angle, ... all zeros for unused params
        var feature = fm.FeatureRevolve2(
            SingleDir: true, IsSolid: true, IsThin: false, IsCut: isCut,
            ReverseDir: false, BothDirectionUpToSameEntity: false,
            Dir1Type: 0, Dir2Type: 0,
            Dir1Angle: angleRad, Dir2Angle: 0,
            OffsetReverse1: false, OffsetReverse2: false,
            OffsetDistance1: 0, OffsetDistance2: 0,
            ThinType: 0, ThinThickness1: 0, ThinThickness2: 0,
            Merge: true, UseFeatScope: false, UseAutoSelect: true)
            ?? throw new InvalidOperationException(
                "Revolve failed — ensure a profile sketch and axis line are selected");

        return new FeatureInfo(feature.Name, isCut ? "RevolveCut" : "Revolve");
    }

    public FeatureInfo Fillet(double radius)
    {
        _cm.EnsureConnected();
        var fm = GetFeatureManager();

        // FeatureFillet(options, radius, filletType=0 constant, overflowType=0, radiusArr, setBackArr, propArr)
        // Returns object — cast to IFeature
        var featureObj = fm.FeatureFillet(0, radius, 0, 0, null, null, null)
            ?? throw new InvalidOperationException("Fillet failed — ensure edges are selected");
        var feature = (IFeature)featureObj;

        return new FeatureInfo(feature.Name, "Fillet");
    }

    public FeatureInfo Chamfer(double distance)
    {
        _cm.EnsureConnected();
        var fm = GetFeatureManager();

        // InsertFeatureChamfer(options, chamferType=0 equal dist, distance, angle, otherDist, ...)
        var feature = fm.InsertFeatureChamfer(0, 0, distance, Math.PI / 4, 0, 0, 0, 0)
            ?? throw new InvalidOperationException("Chamfer failed — ensure edges are selected");

        return new FeatureInfo(feature.Name, "Chamfer");
    }

    public FeatureInfo Shell(double thickness)
    {
        _cm.EnsureConnected();
        var doc = GetModelDoc();

        // InsertFeatureShell(thickness, outward) returns void — retrieve created feature by position
        doc.InsertFeatureShell(thickness, false);
        var feature = doc.IFeatureByPositionReverse(0)
            ?? throw new InvalidOperationException(
                "Shell failed — ensure open faces are selected");

        return new FeatureInfo(feature.Name, "Shell");
    }

    public FeatureInfo SimpleHole(double diameter, double depth, EndCondition endCondition = EndCondition.Blind)
    {
        _cm.EnsureConnected();
        var fm = GetFeatureManager();

        // SimpleHole2: Dia, Sd=true single dir, Flip=false, Dir=true,
        //              T1=endCondition, T2=0, D1=depth, D2=0,
        //              remaining booleans false
        var feature = fm.SimpleHole2(
            Dia: diameter, Sd: true, Flip: false, Dir: true,
            T1: (int)endCondition, T2: 0,
            D1: depth, D2: 0,
            Dchk1: false, Dchk2: false,
            Ddir1: false, Ddir2: false,
            Dang1: 0, Dang2: 0,
            OffsetReverse1: false, OffsetReverse2: false,
            TranslateSurface1: false, TranslateSurface2: false,
            UseFeatScope: false, UseAutoSelect: true,
            AssemblyFeatureScope: false, AutoSelectComponents: false,
            PropagateFeatureToParts: false)
            ?? throw new InvalidOperationException(
                "SimpleHole failed — ensure a face point is selected");

        return new FeatureInfo(feature.Name, "SimpleHole");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private IFeatureManager GetFeatureManager()
    {
        return _cm.SwApp!.FeatureManager
            ?? throw new InvalidOperationException("No active document");
    }

    private IModelDoc2 GetModelDoc()
    {
        return _cm.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException("No active document");
    }
}
