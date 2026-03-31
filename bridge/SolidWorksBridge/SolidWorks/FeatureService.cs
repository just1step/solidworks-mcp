using System.Reflection;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksBridge.SolidWorks;

public record FeatureInfo(string Name, string Type);

public enum EndCondition
{
    Blind = 0,
    ThroughAll = 1,
    MidPlane = 6,
}

public interface IFeatureService
{
    FeatureInfo Extrude(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false);
    FeatureInfo ExtrudeCut(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false);
    FeatureInfo Revolve(double angleDegrees, bool isCut = false);
    FeatureInfo Fillet(double radius);
    FeatureInfo Chamfer(double distance);
    FeatureInfo Shell(double thickness);
}

public class FeatureService : IFeatureService
{
    private const double DefaultDraftAngleRadians = 1.74532925199433E-02;
    private static readonly string[] SketchLikeTypeMarkers = ["sketch", "profile"];

    private readonly ISwConnectionManager _cm;

    public FeatureService(ISwConnectionManager cm)
    {
        _cm = cm ?? throw new ArgumentNullException(nameof(cm));
    }

    public FeatureInfo Extrude(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false)
    {
        _cm.EnsureConnected();
        var doc = GetModelDoc();

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
        var featureManager = GetFeatureManager();
        var topFeatureBefore = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var bodyBefore = CaptureBodySignature(doc);

        NormalizeSketchStateForFeatureCut(doc);

        var returnedFeature = featureManager.FeatureCut4(
            Sd: true,
            Flip: flipDirection,
            Dir: false,
            T1: (int)endCondition,
            T2: 0,
            D1: depth,
            D2: depth,
            Dchk1: false,
            Dchk2: false,
            Ddir1: false,
            Ddir2: false,
            Dang1: DefaultDraftAngleRadians,
            Dang2: DefaultDraftAngleRadians,
            OffsetReverse1: false,
            OffsetReverse2: false,
            TranslateSurface1: false,
            TranslateSurface2: false,
            NormalCut: false,
            UseFeatScope: true,
            UseAutoSelect: true,
            AssemblyFeatureScope: true,
            AutoSelectComponents: true,
            PropagateFeatureToParts: false,
            T0: 0,
            StartOffset: 0,
            FlipStartOffset: false,
            OptimizeGeometry: false);

        var topFeatureAfter = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var bodyAfter = CaptureBodySignature(doc);
        var feature = ResolveCreatedCutFeature(returnedFeature, topFeatureBefore, topFeatureAfter)
            ?? throw new InvalidOperationException(
                $"ExtrudeCut did not create a new cut feature. Before={FormatFeature(topFeatureBefore)}, Returned={FormatFeature(CaptureFeatureSnapshot(returnedFeature))}, After={FormatFeature(topFeatureAfter)}");

        if (!BodyTopologyChanged(bodyBefore, bodyAfter))
        {
            throw new InvalidOperationException(
                $"ExtrudeCut did not change the solid body. BeforeBody={FormatBody(bodyBefore)}, AfterBody={FormatBody(bodyAfter)}, BeforeFeature={FormatFeature(topFeatureBefore)}, Returned={FormatFeature(CaptureFeatureSnapshot(returnedFeature))}, AfterFeature={FormatFeature(topFeatureAfter)}");
        }

        return new FeatureInfo(feature.Name, "ExtrudeCut");
    }

    public FeatureInfo Revolve(double angleDegrees, bool isCut = false)
    {
        _cm.EnsureConnected();
        var fm = GetFeatureManager();
        double angleRad = angleDegrees * Math.PI / 180.0;

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
        var doc = GetModelDoc();

        doc.FeatureFillet5(
            (int)swFeatureFilletOptions_e.swFeatureFilletUniformRadius,
            radius,
            (int)swFeatureFilletType_e.swFeatureFilletType_Simple,
            (int)swFilletOverFlowType_e.swFilletOverFlowType_Default,
            null,
            null,
            null);

        var feature = doc.IFeatureByPositionReverse(0)
            ?? throw new InvalidOperationException("Fillet failed — ensure edges are selected");

        return new FeatureInfo(feature.Name, "Fillet");
    }

    public FeatureInfo Chamfer(double distance)
    {
        _cm.EnsureConnected();
        var fm = GetFeatureManager();

        var feature = fm.InsertFeatureChamfer(0, 0, distance, Math.PI / 4, 0, 0, 0, 0)
            ?? throw new InvalidOperationException("Chamfer failed — ensure edges are selected");

        return new FeatureInfo(feature.Name, "Chamfer");
    }

    public FeatureInfo Shell(double thickness)
    {
        _cm.EnsureConnected();
        var doc = GetModelDoc();

        doc.InsertFeatureShell(thickness, false);
        var feature = doc.IFeatureByPositionReverse(0)
            ?? throw new InvalidOperationException(
                "Shell failed — ensure open faces are selected");

        return new FeatureInfo(feature.Name, "Shell");
    }

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

    private void NormalizeSketchStateForFeatureCut(IModelDoc2 doc)
    {
        if (doc.GetActiveSketch2() == null)
        {
            return;
        }

        doc.ClearSelection2(true);
        var sketchManager = _cm.SwApp!.SketchManager
            ?? throw new InvalidOperationException("No active sketch manager available.");
        sketchManager.InsertSketch(true);
    }

    private static Feature? ResolveCreatedCutFeature(
        Feature? returnedFeature,
        FeatureSnapshot topFeatureBefore,
        FeatureSnapshot topFeatureAfter)
    {
        if (IsNewSolidFeature(topFeatureAfter, topFeatureBefore))
        {
            return topFeatureAfter.Feature;
        }

        var returnedSnapshot = CaptureFeatureSnapshot(returnedFeature);
        if (IsNewSolidFeature(returnedSnapshot, topFeatureBefore))
        {
            return returnedSnapshot.Feature;
        }

        return null;
    }

    private static bool IsNewSolidFeature(FeatureSnapshot candidate, FeatureSnapshot baseline)
    {
        if (candidate.Feature == null)
        {
            return false;
        }

        if (string.Equals(candidate.Name, baseline.Name, StringComparison.Ordinal)
            && string.Equals(candidate.TypeName, baseline.TypeName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsSketchLike(candidate.TypeName);
    }

    private static bool IsSketchLike(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        return SketchLikeTypeMarkers.Any(marker =>
            typeName.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static FeatureSnapshot CaptureFeatureSnapshot(Feature? feature)
    {
        if (feature == null)
        {
            return new FeatureSnapshot(null, null, null);
        }

        return new FeatureSnapshot(feature, feature.Name, SafeGetTypeName(feature));
    }

    private static string? SafeGetTypeName(Feature feature)
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

    private static string FormatFeature(FeatureSnapshot snapshot)
    {
        if (snapshot.Feature == null)
        {
            return "<none>";
        }

        string name = string.IsNullOrWhiteSpace(snapshot.Name) ? "<unnamed>" : snapshot.Name;
        string type = string.IsNullOrWhiteSpace(snapshot.TypeName) ? "<unknown>" : snapshot.TypeName;
        return $"{name} ({type})";
    }

    private static BodySignature? CaptureBodySignature(IModelDoc2 doc)
    {
        if (doc is not IPartDoc part)
        {
            return null;
        }

        var bodies = (object[]?)part.GetBodies2((int)swBodyType_e.swSolidBody, true) ?? Array.Empty<object>();
        var primaryBody = bodies.OfType<IBody2>().FirstOrDefault();
        if (primaryBody == null)
        {
            return null;
        }

        int faceCount = ((object[]?)primaryBody.GetFaces() ?? Array.Empty<object>()).Length;
        int edgeCount = ((object[]?)primaryBody.GetEdges() ?? Array.Empty<object>()).Length;
        int vertexCount = ((object[]?)primaryBody.GetVertices() ?? Array.Empty<object>()).Length;
        return new BodySignature(bodies.Length, faceCount, edgeCount, vertexCount);
    }

    private static bool BodyTopologyChanged(BodySignature? before, BodySignature? after)
    {
        if (before == null || after == null)
        {
            return true;
        }

        return before.Value != after.Value;
    }

    private static string FormatBody(BodySignature? signature)
    {
        if (signature == null)
        {
            return "<unavailable>";
        }

        return $"bodies={signature.Value.BodyCount}, faces={signature.Value.FaceCount}, edges={signature.Value.EdgeCount}, vertices={signature.Value.VertexCount}";
    }

    private readonly record struct BodySignature(int BodyCount, int FaceCount, int EdgeCount, int VertexCount);
    private readonly record struct FeatureSnapshot(Feature? Feature, string? Name, string? TypeName);
}
