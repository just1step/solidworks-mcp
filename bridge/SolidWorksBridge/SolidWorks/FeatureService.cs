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
        EnsureAvailableSketchHasClosedProfile(doc, "IModelDoc2.FeatureBoss2", "extrude");
        var topFeatureBefore = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var bodyBefore = CaptureBodySignature(doc);

        doc.FeatureBoss2(
            Sd: true, Flip: flipDirection, Dir: !flipDirection,
            T1: (int)endCondition, T2: 0,
            D1: depth, D2: 0,
            Dchk1: false, Dchk2: false,
            Ddir1: false, Ddir2: false,
            Dang1: 0, Dang2: 0,
            OffsetReverse1: false, OffsetReverse2: false,
            TranslateSurface1: false, TranslateSurface2: false);

        var topFeatureAfter = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var bodyAfter = CaptureBodySignature(doc);
        var feature = ResolveCreatedSolidFeature(topFeatureBefore, topFeatureAfter)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "IModelDoc2.FeatureBoss2",
                "SolidWorks did not create a new extrude feature.",
                new Dictionary<string, object?>
                {
                    ["beforeFeature"] = FormatFeature(topFeatureBefore),
                    ["afterFeature"] = FormatFeature(topFeatureAfter),
                    ["depth"] = depth,
                });

        if (!BodyTopologyChanged(bodyBefore, bodyAfter))
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                "IModelDoc2.FeatureBoss2",
                "SolidWorks did not change the solid body during extrude.",
                new Dictionary<string, object?>
                {
                    ["beforeBody"] = FormatBody(bodyBefore),
                    ["afterBody"] = FormatBody(bodyAfter),
                    ["feature"] = FormatFeature(topFeatureAfter),
                });
        }

        return new FeatureInfo(feature.Name, "Extrude");
    }

    public FeatureInfo ExtrudeCut(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false)
    {
        _cm.EnsureConnected();
        var doc = GetModelDoc();
        EnsureAvailableSketchHasClosedProfile(doc, "IFeatureManager.FeatureCut4", "cut extrude");
        var featureManager = GetFeatureManager();
        var topFeatureBefore = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var featureTreeBefore = CaptureTopLevelFeatureSnapshots(doc);
        var bodyBefore = CaptureBodySignature(doc);

        NormalizeSketchStateForFeatureCut(doc);

        var returnedFeature = featureManager.FeatureCut4(
            Sd: true,
            Flip: false,
            Dir: flipDirection,
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
        var featureTreeAfter = CaptureTopLevelFeatureSnapshots(doc);
        var bodyAfter = CaptureBodySignature(doc);
        var feature = ResolveCreatedCutFeature(returnedFeature, topFeatureBefore, topFeatureAfter, featureTreeBefore, featureTreeAfter)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "IFeatureManager.FeatureCut4",
                "SolidWorks did not create a new cut feature.",
                new Dictionary<string, object?>
                {
                    ["beforeFeature"] = FormatFeature(topFeatureBefore),
                    ["returnedFeature"] = FormatFeature(CaptureFeatureSnapshot(returnedFeature)),
                    ["afterFeature"] = FormatFeature(topFeatureAfter),
                });

        return new FeatureInfo(feature.Name, "ExtrudeCut");
    }

    public FeatureInfo Revolve(double angleDegrees, bool isCut = false)
    {
        _cm.EnsureConnected();
        var doc = GetModelDoc();
        EnsureAvailableSketchHasClosedProfile(doc, "IFeatureManager.FeatureRevolve2", isCut ? "cut revolve" : "revolve");
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

    private static void EnsureAvailableSketchHasClosedProfile(IModelDoc2 doc, string apiName, string operationName)
    {
        var sketchContext = ResolveSketchForProfileValidation(doc);
        if (sketchContext == null)
        {
            return;
        }

        var (sketch, source) = sketchContext.Value;

        var segments = (object[]?)sketch.GetSketchSegments() ?? Array.Empty<object>();
        int segmentCount = segments.Length;
        int contourCount = sketch.GetSketchContourCount();
        int regionCount = sketch.GetSketchRegionCount();
        var contourObjects = (object[]?)sketch.GetSketchContours() ?? Array.Empty<object>();
        var contours = contourObjects.OfType<ISketchContour>().ToList();
        int closedContourCount = contours.Count(contour => contour.IsClosed());
        int openContourCount = Math.Max(contourCount - closedContourCount, 0);

        if (segmentCount == 0)
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                apiName,
                $"The active sketch is empty, so {operationName} cannot create a feature.",
                new Dictionary<string, object?>
                {
                    ["sketchSource"] = source,
                    ["segmentCount"] = segmentCount,
                    ["contourCount"] = contourCount,
                    ["closedContourCount"] = closedContourCount,
                    ["regionCount"] = regionCount,
                });
        }

        if (openContourCount > 0)
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                apiName,
                $"The active sketch contains open contours, so {operationName} cannot create a solid feature. Close every open loop first or use a closed-shape sketch tool such as AddCircle, AddRectangle, or AddPolygon.",
                new Dictionary<string, object?>
                {
                    ["sketchSource"] = source,
                    ["segmentCount"] = segmentCount,
                    ["contourCount"] = contourCount,
                    ["closedContourCount"] = closedContourCount,
                    ["openContourCount"] = openContourCount,
                    ["regionCount"] = regionCount,
                });
        }

        if (regionCount == 0)
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                apiName,
                $"The active sketch does not contain any valid sketch regions, so {operationName} cannot create a solid feature. The contours may overlap, self-intersect, or otherwise fail to form a usable profile.",
                new Dictionary<string, object?>
                {
                    ["sketchSource"] = source,
                    ["segmentCount"] = segmentCount,
                    ["contourCount"] = contourCount,
                    ["closedContourCount"] = closedContourCount,
                    ["openContourCount"] = openContourCount,
                    ["regionCount"] = regionCount,
                });
        }
    }

    private static (ISketch Sketch, string Source)? ResolveSketchForProfileValidation(IModelDoc2 doc)
    {
        if (doc.GetActiveSketch2() is ISketch activeSketch)
        {
            return (activeSketch, "active-sketch");
        }

        var topFeature = doc.IFeatureByPositionReverse(0);
        if (topFeature == null || !IsSketchLike(SafeGetTypeName(topFeature)))
        {
            return null;
        }

        try
        {
            if (topFeature.GetSpecificFeature2() is ISketch topSketch)
            {
                return (topSketch, "top-profile-feature");
            }
        }
        catch (COMException)
        {
            return null;
        }
        catch (TargetInvocationException)
        {
            return null;
        }

        return null;
    }

    private void NormalizeSketchStateForFeatureCut(IModelDoc2 doc)
    {
        string? sketchName = ResolveSketchSelectionTargetName(doc);
        if (string.IsNullOrWhiteSpace(sketchName) && doc.GetActiveSketch2() == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sketchName))
        {
            doc.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, false, 0, null, 0);
        }

        doc.ClearSelection2(true);

        var sketchManager = _cm.SwApp!.SketchManager
            ?? throw new InvalidOperationException("No active sketch manager available.");
        if (doc.GetActiveSketch2() != null)
        {
            sketchManager.InsertSketch(true);
        }
        else if (!string.IsNullOrWhiteSpace(sketchName))
        {
            sketchManager.InsertSketch(true);
        }

        if (string.IsNullOrWhiteSpace(sketchName))
        {
            return;
        }

        doc.ClearSelection2(true);
        bool selected = doc.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, false, 0, null, 0);
        if (!selected)
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                "IFeatureManager.FeatureCut4",
                "SolidWorks exited sketch edit mode but did not reselect the sketch feature required for cut extrude.",
                new Dictionary<string, object?>
                {
                    ["sketchName"] = sketchName,
                });
        }
    }

    private static string? ResolveSketchSelectionTargetName(IModelDoc2 doc)
    {
        if (doc.GetActiveSketch2() is ISketch activeSketch)
        {
            string? activeName = TryGetSketchName(activeSketch);
            if (!string.IsNullOrWhiteSpace(activeName))
            {
                return activeName;
            }
        }

        var topFeature = doc.IFeatureByPositionReverse(0);
        if (topFeature != null && IsSketchLike(SafeGetTypeName(topFeature)))
        {
            return topFeature.Name;
        }

        return null;
    }

    private static string? TryGetSketchName(ISketch sketch)
    {
        try
        {
            return sketch.GetType().InvokeMember(
                "GetName",
                BindingFlags.InvokeMethod,
                binder: null,
                target: sketch,
                args: null) as string;
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

    private static Feature? ResolveCreatedCutFeature(
        Feature? returnedFeature,
        FeatureSnapshot topFeatureBefore,
        FeatureSnapshot topFeatureAfter,
        IReadOnlyList<FeatureSnapshot> featureTreeBefore,
        IReadOnlyList<FeatureSnapshot> featureTreeAfter)
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

        return ResolveNewSolidFeatureFromTree(featureTreeBefore, featureTreeAfter);
    }

    private static Feature? ResolveNewSolidFeatureFromTree(
        IReadOnlyList<FeatureSnapshot> featureTreeBefore,
        IReadOnlyList<FeatureSnapshot> featureTreeAfter)
    {
        var remainingBeforeCounts = new Dictionary<(string? Name, string? TypeName), int>();
        foreach (var snapshot in featureTreeBefore)
        {
            var key = (snapshot.Name, snapshot.TypeName);
            remainingBeforeCounts[key] = remainingBeforeCounts.TryGetValue(key, out int count)
                ? count + 1
                : 1;
        }

        for (int index = featureTreeAfter.Count - 1; index >= 0; index--)
        {
            var snapshot = featureTreeAfter[index];
            var key = (snapshot.Name, snapshot.TypeName);
            if (remainingBeforeCounts.TryGetValue(key, out int count) && count > 0)
            {
                remainingBeforeCounts[key] = count - 1;
                continue;
            }

            if (snapshot.Feature != null && !IsSketchLike(snapshot.TypeName))
            {
                return snapshot.Feature;
            }
        }

        return null;
    }

    private static Feature? ResolveCreatedSolidFeature(
        FeatureSnapshot topFeatureBefore,
        FeatureSnapshot topFeatureAfter)
    {
        return IsNewSolidFeature(topFeatureAfter, topFeatureBefore)
            ? topFeatureAfter.Feature
            : null;
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

    private static IReadOnlyList<FeatureSnapshot> CaptureTopLevelFeatureSnapshots(IModelDoc2 doc)
    {
        var snapshots = new List<FeatureSnapshot>();
        for (var feature = doc.FirstFeature() as Feature; feature != null; feature = feature.GetNextFeature() as Feature)
        {
            snapshots.Add(CaptureFeatureSnapshot(feature));
        }

        return snapshots;
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
