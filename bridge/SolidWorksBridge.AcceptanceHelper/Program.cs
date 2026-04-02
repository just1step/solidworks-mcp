using System.Text.Json;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.AcceptanceHelper;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static int Main(string[] args)
    {
        int exitCode = 0;
        var thread = new Thread(() =>
        {
            try
            {
                var result = Execute(args);
                Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                exitCode = 1;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return exitCode;
    }

    private static object Execute(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("A command is required.");
        }

        using var session = new AcceptanceSession();

        return args[0] switch
        {
            "reset-session" => session.ResetSession(),
            "create-saved-part" => session.CreateSavedBoxPart(),
            "prepare-face-cut" => session.PrepareFaceCut(),
            "prepare-revolve" => session.PrepareRevolve(),
            "prepare-fillet" => session.PrepareFillet(),
            "prepare-chamfer" => session.PrepareChamfer(),
            "prepare-shell" => session.PrepareShell(),
            "prepare-mate-coincident" => session.PrepareMate(MatePreparationKind.Coincident),
            "prepare-mate-parallel" => session.PrepareMate(MatePreparationKind.Parallel),
            "prepare-mate-distance" => session.PrepareMate(MatePreparationKind.Distance),
            "prepare-mate-angle" => session.PrepareMate(MatePreparationKind.Angle),
            "prepare-mate-concentric" => session.PrepareMate(MatePreparationKind.Concentric),
            "activate-document" => session.ActivateExistingDocument(args[1]),
            "show-view" => session.ShowView(Enum.Parse<SwStandardView>(args[1], true)),
            "export-isolated-views" => session.ExportIsolatedViews(
                args[1],
                [args[2], args[3]],
                args[4]),
            "sketch-use-edge3" => session.SketchUseEdge3(args.Length > 1 ? bool.Parse(args[1]) : false, args.Length > 2 ? bool.Parse(args[2]) : true),
            "convert-face-loops" => session.ConvertFaceEdgesAndInnerLoopsToSketch(
                int.Parse(args[1]),
                args.Length > 2 ? bool.Parse(args[2]) : false,
                args.Length > 3 ? bool.Parse(args[3]) : true),
            "convert-face-edges-to-sketch" => session.ConvertFaceEdgesToSketch(
                int.Parse(args[1]),
                args.Length > 2 ? bool.Parse(args[2]) : true),
            "project-face-edges-to-sketch" => session.ProjectFaceEdgesToSketch(
                int.Parse(args[1]),
                args.Length > 2 ? bool.Parse(args[2]) : true),
            "convert-face-cut" => session.ConvertFaceToSketchAndCut(
                int.Parse(args[1]),
                double.Parse(args[2]),
                args.Length > 3 && bool.Parse(args[3]),
                args.Length > 4 ? bool.Parse(args[4]) : false,
                args.Length > 5 ? bool.Parse(args[5]) : true),
            "convert-face-edges-cut" => session.ConvertFaceEdgesToSketchAndCut(
                int.Parse(args[1]),
                double.Parse(args[2]),
                args.Length > 3 && bool.Parse(args[3]),
                args.Length > 4 ? bool.Parse(args[4]) : true),
            "convert-selected-face-edges-cut" => session.ConvertSelectedFaceEdgesToSketchAndCut(
                double.Parse(args[1]),
                args.Length > 2 && bool.Parse(args[2]),
                args.Length > 3 ? bool.Parse(args[3]) : true),
            "raw-cut-selected-sketch" => session.RawCutSelectedSketch(
                double.Parse(args[1]),
                args.Length > 2 && bool.Parse(args[2])),
            "inspect-selected-sketch-profile" => session.InspectSelectedSketchProfile(),
            "inspect-selected-sketch-regions" => session.InspectSelectedSketchRegions(),
            "select-feature-by-name" => session.SelectFeatureByName(args[1]),
            "delete-feature-by-name" => session.DeleteFeatureByName(args[1]),
            "edit-selected-sketch" => session.EditSelectedSketch(),
            "probe-edit-selected-sketch" => session.ProbeEditSelectedSketch(),
            "raw-cut-selected-sketch-region" => session.RawCutSelectedSketchRegion(
                int.Parse(args[1]),
                double.Parse(args[2]),
                args.Length > 3 && bool.Parse(args[3])),
            "raw-cut-active-sketch" => session.RawCutActiveSketch(
                double.Parse(args[1]),
                args.Length > 2 && bool.Parse(args[2])),
            "raw-cut-active-sketch-explicit" => session.RawCutActiveSketchExplicit(
                double.Parse(args[1]),
                bool.Parse(args[2]),
                bool.Parse(args[3])),
            "replay-vba-featurecut" => session.ReplayRecordedVbaFeatureCut(args[1]),
            "raw-cut-sketch-region-by-point" => session.RawCutSketchRegionByPoint(
                double.Parse(args[1]),
                double.Parse(args[2]),
                double.Parse(args[3]),
                double.Parse(args[4]),
                args.Length > 5 && bool.Parse(args[5])),
            "rectangle-face-cut" => session.CutRectangleOnFace(
                int.Parse(args[1]),
                double.Parse(args[2]),
                double.Parse(args[3]),
                double.Parse(args[4]),
                double.Parse(args[5]),
                double.Parse(args[6]),
                args.Length > 7 && bool.Parse(args[7])),
            "ray-convert-cut" => session.CutByRayAndConvertEntities(
                double.Parse(args[1]),
                double.Parse(args[2]),
                double.Parse(args[3]),
                double.Parse(args[4]),
                double.Parse(args[5]),
                double.Parse(args[6]),
                double.Parse(args[7]),
                int.Parse(args[8]),
                args.Length > 9 && bool.Parse(args[9]),
                args.Length > 10 ? int.Parse(args[10]) : 0,
                args.Length > 11 ? int.Parse(args[11]) : 0,
                args.Length > 12 ? double.Parse(args[12]) : 0.002,
                args.Length > 13 ? double.Parse(args[13]) : 0.01),
            "debug-ray-select" => session.DebugRaySelection(
                double.Parse(args[1]),
                double.Parse(args[2]),
                double.Parse(args[3]),
                double.Parse(args[4]),
                double.Parse(args[5]),
                double.Parse(args[6]),
                double.Parse(args[7]),
                int.Parse(args[8]),
                args.Length > 9 && bool.Parse(args[9]),
                args.Length > 10 ? int.Parse(args[10]) : 0,
                args.Length > 11 ? int.Parse(args[11]) : 0),
            "inspect-face-outline" => session.InspectFaceOutline(
                int.Parse(args[1]),
                args.Length > 2 && bool.Parse(args[2]),
                args.Length > 3 && bool.Parse(args[3])),
            "list-entities" => session.ListEntities(
                NormalizeEntityTypeArg(args[1]),
                NormalizeOptionalArg(args[2])),
            "measure-entities" => session.MeasureEntities(
                Enum.Parse<SelectableEntityType>(args[1], true),
                int.Parse(args[2]),
                NormalizeOptionalArg(args[3]),
                Enum.Parse<SelectableEntityType>(args[4], true),
                int.Parse(args[5]),
                NormalizeOptionalArg(args[6]),
                args.Length > 7 ? int.Parse(args[7]) : 1),
            "list-assembly-components" => session.ListAssemblyComponents(args[1]),
            "replace-component" => session.ReplaceComponentInAssembly(args[1], args[2], args[3], args.Length > 4 ? args[4] : string.Empty),
            "save-active-document" => session.SaveActiveDocument(),
            _ => throw new ArgumentException($"Unknown command: {args[0]}")
        };
    }

    private static string? NormalizeOptionalArg(string value)
        => value == "-" ? null : value;

    private static SelectableEntityType? NormalizeEntityTypeArg(string value)
        => value == "-" ? null : Enum.Parse<SelectableEntityType>(value, true);
}

internal enum MatePreparationKind
{
    Coincident,
    Parallel,
    Distance,
    Angle,
    Concentric,
}

internal sealed class AcceptanceSession : IDisposable
{
    private sealed record AssemblyComponentInstance(IComponent2 Component, string HierarchyPath);

    private readonly SwConnectionManager _manager;
    private readonly DocumentService _documents;
    private readonly SelectionService _selection;
    private readonly SketchService _sketch;
    private readonly FeatureService _feature;
    private readonly AssemblyService _assembly;

    public AcceptanceSession()
    {
        _manager = new SwConnectionManager(new SwComConnector());
        _manager.Connect();
        _documents = new DocumentService(_manager);
        _selection = new SelectionService(_manager);
        _sketch = new SketchService(_manager);
        _feature = new FeatureService(_manager);
        _assembly = new AssemblyService(_manager);
    }

    public object ResetSession()
    {
        _manager.EnsureConnected();
        _manager.SwApp!.CloseAllDocuments(false);
        return new { reset = true };
    }

    public object CreateSavedBoxPart()
    {
        ResetSession();
        CreateBoxPart(0.02, 0.02, 0.01);

        string path = Path.Combine(Path.GetTempPath(), $"SwMcpAcceptance_{Guid.NewGuid():N}.sldprt");
        SaveActiveDocumentAs(path);
        return new { path };
    }

    public object PrepareRevolve()
    {
        ResetSession();
        _documents.NewDocument(SwDocType.Part);
        SelectFrontPlane();
        _sketch.InsertSketch();

        var sketchManager = _manager.SwApp!.SketchManager!;
        var centerLine = sketchManager.CreateCenterLine(0, -0.03, 0, 0, 0.03, 0)
            ?? throw new InvalidOperationException("Failed to create revolve centerline.");
        _ = sketchManager.CreateCornerRectangle(0.01, -0.02, 0, 0.03, 0.02, 0)
            ?? throw new InvalidOperationException("Failed to create revolve profile.");

        var doc = RequireActiveDoc();
        bool selected = doc.Extension.SelectByID2("", "SKETCHSEGMENT", 0, 0, 0, false, 0, null, 0);
        if (!selected)
        {
            throw new InvalidOperationException("Failed to select the revolve axis sketch segment.");
        }

        EnsureSelectionCountAtLeast(1, "revolve setup");
        return ActiveDocumentInfo();
    }

    public object PrepareFillet()
    {
        ResetSession();
        CreateBoxPart(0.06, 0.04, 0.02);
        SelectFirstEdge();
        return ActiveDocumentInfo();
    }

    public object PrepareChamfer()
    {
        ResetSession();
        CreateBoxPart(0.06, 0.04, 0.02);
        SelectFirstEdge();
        return ActiveDocumentInfo();
    }

    public object PrepareShell()
    {
        ResetSession();
        CreateBoxPart(0.06, 0.04, 0.02);
        SelectFirstPlanarFace();
        return ActiveDocumentInfo();
    }

    public object PrepareFaceCut()
    {
        ResetSession();
        CreateBoxPart(0.06, 0.04, 0.02);
        SelectTopPlanarFace();
        return ActiveDocumentInfo();
    }

    public object PrepareMate(MatePreparationKind kind)
    {
        ResetSession();

        return kind == MatePreparationKind.Concentric
            ? PrepareConcentricMate()
            : PreparePlanarMate(kind);
    }

    public object ReplaceComponentInAssembly(string assemblyPath, string hierarchyPath, string replacementFilePath, string configName = "")
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("assemblyPath is required.", nameof(assemblyPath));
        }

        if (string.IsNullOrWhiteSpace(hierarchyPath))
        {
            throw new ArgumentException("hierarchyPath is required.", nameof(hierarchyPath));
        }

        if (string.IsNullOrWhiteSpace(replacementFilePath))
        {
            throw new ArgumentException("replacementFilePath is required.", nameof(replacementFilePath));
        }

        ActivateDocument(assemblyPath);
        var result = _assembly.ReplaceComponent(hierarchyPath, replacementFilePath, configName);
        var saveResult = _documents.SaveDocument(assemblyPath);

        return new
        {
            assemblyPath,
            hierarchyPath,
            replacementFilePath,
            configName,
            replaceResult = result,
            saveResult,
        };
    }

    public object ConvertFaceEdgesAndInnerLoopsToSketch(int faceIndex, bool chain = false, bool innerLoops = true)
    {
        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        if (active.Type != (int)SwDocType.Part)
        {
            throw new InvalidOperationException("convert-face-loops requires an active part document.");
        }

        var faceEdges = OpenSketchAndSelectFaceEdges(faceIndex);
        _sketch.SketchUseEdge3(chain, innerLoops);

        var doc = RequireActiveDoc();
        var activeSketch = doc.GetActiveSketch2() as ISketch;
        var topFeature = doc.IFeatureByPositionReverse(0);

        return new
        {
            active,
            faceIndex,
            chain,
            innerLoops,
            edgeCount = faceEdges.Length,
            sketchActive = activeSketch != null,
            activeSketchName = TryGetComName(activeSketch),
            topFeature = CaptureFeatureSnapshot(topFeature),
        };
    }

    public object ConvertFaceToSketchAndCut(int faceIndex, double depth, bool flipDirection = false, bool chain = false, bool innerLoops = true)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        if (active.Type != (int)SwDocType.Part)
        {
            throw new InvalidOperationException("convert-face-cut requires an active part document.");
        }

        var faceEdges = OpenSketchAndSelectFaceEdges(faceIndex);
        _sketch.SketchUseEdge3(chain, innerLoops);
        var feature = _feature.ExtrudeCut(depth, EndCondition.Blind, flipDirection);

        return new
        {
            active,
            faceIndex,
            depth,
            flipDirection,
            chain,
            innerLoops,
            edgeCount = faceEdges.Length,
            feature,
        };
    }

    public object ConvertFaceEdgesToSketchAndCut(int faceIndex, double depth, bool flipDirection = false, bool innerLoops = true)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        if (active.Type != (int)SwDocType.Part)
        {
            throw new InvalidOperationException("convert-face-edges-cut requires an active part document.");
        }

        var faceEdges = OpenSketchAndSelectFaceEdges(faceIndex);
        _sketch.SketchUseEdge3(chain: false, innerLoops);
        bool actualFlipDirection = flipDirection;
        object feature;
        try
        {
            feature = _feature.ExtrudeCut(depth, EndCondition.Blind, flipDirection);
        }
        catch (SolidWorksApiException ex) when (ShouldRetryOppositeDirection(ex))
        {
            actualFlipDirection = !flipDirection;
            feature = _feature.ExtrudeCut(depth, EndCondition.Blind, actualFlipDirection);
        }

        return new
        {
            active,
            faceIndex,
            depth,
            requestedFlipDirection = flipDirection,
            actualFlipDirection,
            innerLoops,
            edgeCount = faceEdges.Length,
            feature,
        };
    }

    public object ConvertFaceEdgesToSketch(int faceIndex, bool innerLoops = true)
    {
        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        if (active.Type != (int)SwDocType.Part)
        {
            throw new InvalidOperationException("convert-face-edges-to-sketch requires an active part document.");
        }

        var part = RequireActiveDoc();
        var faceEdges = OpenSketchAndSelectFaceEdges(faceIndex);
        _sketch.SketchUseEdge3(chain: false, innerLoops);

        var activeSketch = part.GetActiveSketch2() as ISketch;
        var topFeature = part.IFeatureByPositionReverse(0);
        return new
        {
            active,
            faceIndex,
            innerLoops,
            edgeCount = faceEdges.Length,
            sketchActive = activeSketch != null,
            activeSketchName = TryGetComName(activeSketch),
            topFeature = CaptureFeatureSnapshot(topFeature),
        };
    }

    public object ProjectFaceEdgesToSketch(int faceIndex, bool innerLoops = true)
    {
        return ConvertFaceEdgesToSketch(faceIndex, innerLoops);
    }

    public object ConvertSelectedFaceEdgesToSketchAndCut(double depth, bool flipDirection = false, bool innerLoops = true)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        if (active.Type != (int)SwDocType.Part)
        {
            throw new InvalidOperationException("convert-selected-face-edges-cut requires an active part document.");
        }

        var part = RequireActiveDoc();
        var selectedFace = part.ISelectionManager?.GetSelectedObject6(1, -1) as IFace2
            ?? throw new InvalidOperationException("Select exactly one planar face before running convert-selected-face-edges-cut.");

        _sketch.InsertSketch();
        var faceEdges = SelectEdgesForSketchUseEdge3(part, selectedFace);
        _sketch.SketchUseEdge3(chain: false, innerLoops);
        bool actualFlipDirection = flipDirection;
        object feature;
        try
        {
            feature = _feature.ExtrudeCut(depth, EndCondition.Blind, flipDirection);
        }
        catch (SolidWorksApiException ex) when (ShouldRetryOppositeDirection(ex))
        {
            actualFlipDirection = !flipDirection;
            feature = _feature.ExtrudeCut(depth, EndCondition.Blind, actualFlipDirection);
        }

        return new
        {
            active,
            depth,
            requestedFlipDirection = flipDirection,
            actualFlipDirection,
            innerLoops,
            edgeCount = faceEdges.Length,
            feature,
        };
    }

    public object RawCutSelectedSketch(double depth, bool flipDirection = false)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        if (active.Type != (int)SwDocType.Part)
        {
            throw new InvalidOperationException("raw-cut-selected-sketch requires an active part document.");
        }

        var doc = RequireActiveDoc();
        var featureManager = _manager.SwApp!.FeatureManager
            ?? throw new InvalidOperationException("No active feature manager available.");

        var selectionsBefore = DescribeSelections();
        var topFeatureBefore = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var bodyBefore = CaptureBodySignature(doc);

        var returnedFeature = featureManager.FeatureCut4(
            Sd: true,
            Flip: flipDirection,
            Dir: !flipDirection,
            T1: 0,
            T2: 0,
            D1: depth,
            D2: depth,
            Dchk1: false,
            Dchk2: false,
            Ddir1: false,
            Ddir2: false,
            Dang1: 1.74532925199433E-02,
            Dang2: 1.74532925199433E-02,
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
        var selectionsAfter = DescribeSelections();

        return new
        {
            active,
            depth,
            flipDirection,
            selectionsBefore,
            selectionsAfter,
            topFeatureBefore,
            returnedFeature = CaptureFeatureSnapshot(returnedFeature),
            topFeatureAfter,
            bodyBefore,
            bodyAfter,
        };
    }

    public object InspectSelectedSketchProfile()
    {
        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        if (active.Type != (int)SwDocType.Part)
        {
            throw new InvalidOperationException("inspect-selected-sketch-profile requires an active part document.");
        }

        var selectionManager = RequireActiveDoc().ISelectionManager
            ?? throw new InvalidOperationException("No active selection manager available.");
        var selected = selectionManager.GetSelectedObject6(1, -1)
            ?? throw new InvalidOperationException("Select a sketch before running inspect-selected-sketch-profile.");

        ISketch sketch = selected switch
        {
            ISketch directSketch => directSketch,
            Feature feature => feature.GetSpecificFeature2() as ISketch
                ?? throw new InvalidOperationException("The selected feature is not a sketch."),
            _ => throw new InvalidOperationException("The selected object is not a sketch."),
        };

        var segments = (object[]?)sketch.GetSketchSegments() ?? Array.Empty<object>();
        int contourCount = sketch.GetSketchContourCount();
        int regionCount = sketch.GetSketchRegionCount();
        var contours = ((object[]?)sketch.GetSketchContours() ?? Array.Empty<object>())
            .OfType<ISketchContour>()
            .ToArray();
        int closedContourCount = contours.Count(contour => contour.IsClosed());

        return new
        {
            active,
            selectedTypeCode = selectionManager.GetSelectedObjectType3(1, -1),
            selectedName = TryGetComName(selected),
            segmentCount = segments.Length,
            contourCount,
            closedContourCount,
            openContourCount = Math.Max(contourCount - closedContourCount, 0),
            regionCount,
        };
    }

    public object SelectFeatureByName(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            throw new ArgumentException("featureName is required.", nameof(featureName));
        }

        var doc = RequireActiveDoc();
        doc.ClearSelection2(true);

        for (var feature = doc.FirstFeature() as Feature; feature != null; feature = feature.GetNextFeature() as Feature)
        {
            if (!string.Equals(feature.Name, featureName, StringComparison.Ordinal))
            {
                continue;
            }

            bool selected = feature.Select2(false, -1);
            return new
            {
                featureName,
                featureType = feature.GetTypeName2(),
                selected,
                selections = DescribeSelections(),
            };
        }

        throw new InvalidOperationException($"Feature '{featureName}' was not found in the active document.");
    }

    public object DeleteFeatureByName(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            throw new ArgumentException("featureName is required.", nameof(featureName));
        }

        var doc = RequireActiveDoc();
        doc.ClearSelection2(true);

        for (var feature = doc.FirstFeature() as Feature; feature != null; feature = feature.GetNextFeature() as Feature)
        {
            if (!string.Equals(feature.Name, featureName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!feature.Select2(false, -1))
            {
                throw new InvalidOperationException($"Feature '{featureName}' could not be selected for deletion.");
            }

            doc.EditDelete();
            bool deleted = !TopLevelFeatureExists(doc, featureName);
            return new
            {
                featureName,
                deleted,
                selections = DescribeSelections(),
            };
        }

        throw new InvalidOperationException($"Feature '{featureName}' was not found in the active document.");
    }

    public object InspectSelectedSketchRegions()
    {
        var doc = RequireActiveDoc();
        var selectionManager = doc.ISelectionManager
            ?? throw new InvalidOperationException("No active selection manager available.");
        var selected = selectionManager.GetSelectedObject6(1, -1)
            ?? throw new InvalidOperationException("Select a sketch before running inspect-selected-sketch-regions.");

        ISketch sketch = selected switch
        {
            ISketch directSketch => directSketch,
            Feature feature => feature.GetSpecificFeature2() as ISketch
                ?? throw new InvalidOperationException("The selected feature is not a sketch."),
            _ => throw new InvalidOperationException("The selected object is not a sketch."),
        };

        var regions = GetSketchRegions(sketch)
            .Select((region, index) => new
            {
                index,
                box = InvokeComDoubleArray(region, "GetBox"),
                runtimeType = region.GetType().FullName,
            })
            .ToArray();

        return new
        {
            selectedName = TryGetComName(selected),
            regionCount = regions.Length,
            regions,
        };
    }

    public object EditSelectedSketch()
    {
        var doc = RequireActiveDoc();
        var sketchManager = _manager.SwApp!.SketchManager
            ?? throw new InvalidOperationException("No active sketch manager available.");

        sketchManager.InsertSketch(true);

        var activeSketch = doc.GetActiveSketch2() as ISketch;
        return new
        {
            activeSketchName = TryGetComName(activeSketch),
            isEditing = activeSketch != null,
            selections = DescribeSelections(),
        };
    }

    public object ProbeEditSelectedSketch()
    {
        var doc = RequireActiveDoc();
        var attempts = new List<object>();

        foreach (var methodName in new[] { "EditSketchOrSingleSketchFeature", "EditSketch" })
        {
            try
            {
                var result = ((object)doc).GetType().InvokeMember(
                    methodName,
                    System.Reflection.BindingFlags.InvokeMethod,
                    binder: null,
                    target: doc,
                    args: null);

                var activeSketch = doc.GetActiveSketch2() as ISketch;
                attempts.Add(new
                {
                    methodName,
                    invoked = true,
                    result = result?.ToString(),
                    activeSketchName = TryGetComName(activeSketch),
                    isEditing = activeSketch != null,
                });

                if (activeSketch != null)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                attempts.Add(new
                {
                    methodName,
                    invoked = false,
                    error = ex.GetType().Name,
                    message = ex.Message,
                });
            }
        }

        return new
        {
            attempts,
            finalActiveSketchName = TryGetComName(doc.GetActiveSketch2() as ISketch),
            isEditing = doc.GetActiveSketch2() != null,
            selections = DescribeSelections(),
        };
    }

    public object RawCutSelectedSketchRegion(int regionIndex, double depth, bool flipDirection = false)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var doc = RequireActiveDoc();
        var selectionManager = doc.ISelectionManager
            ?? throw new InvalidOperationException("No active selection manager available.");
        var selected = selectionManager.GetSelectedObject6(1, -1)
            ?? throw new InvalidOperationException("Select a sketch before running raw-cut-selected-sketch-region.");

        ISketch sketch = selected switch
        {
            ISketch directSketch => directSketch,
            Feature feature => feature.GetSpecificFeature2() as ISketch
                ?? throw new InvalidOperationException("The selected feature is not a sketch."),
            _ => throw new InvalidOperationException("The selected object is not a sketch."),
        };

        var regions = GetSketchRegions(sketch);
        if (regionIndex < 0 || regionIndex >= regions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(regionIndex), $"Valid range is 0 to {regions.Count - 1}.");
        }

        selectionManager.EnableContourSelection = true;
        try
        {
            doc.ClearSelection2(true);
            SelectComObject(regions[regionIndex], append: false, mark: 0);

            var featureManager = _manager.SwApp!.FeatureManager
                ?? throw new InvalidOperationException("No active feature manager available.");
            var topFeatureBefore = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
            var bodyBefore = CaptureBodySignature(doc);

            var returnedFeature = featureManager.FeatureCut4(
                Sd: true,
                Flip: flipDirection,
                Dir: !flipDirection,
                T1: 0,
                T2: 0,
                D1: depth,
                D2: depth,
                Dchk1: false,
                Dchk2: false,
                Ddir1: false,
                Ddir2: false,
                Dang1: 1.74532925199433E-02,
                Dang2: 1.74532925199433E-02,
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
            return new
            {
                regionIndex,
                depth,
                flipDirection,
                returnedFeature = CaptureFeatureSnapshot(returnedFeature),
                topFeatureBefore,
                topFeatureAfter,
                bodyBefore,
                bodyAfter,
            };
        }
        finally
        {
            selectionManager.EnableContourSelection = false;
        }
    }

    public object RawCutActiveSketch(double depth, bool flipDirection = false)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var doc = RequireActiveDoc();
        var activeSketch = doc.GetActiveSketch2() as ISketch
            ?? throw new InvalidOperationException("No active sketch is being edited.");
        var featureManager = _manager.SwApp!.FeatureManager
            ?? throw new InvalidOperationException("No active feature manager available.");

        var topFeatureBefore = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var bodyBefore = CaptureBodySignature(doc);

        var returnedFeature = featureManager.FeatureCut4(
            Sd: true,
            Flip: flipDirection,
            Dir: !flipDirection,
            T1: 0,
            T2: 0,
            D1: depth,
            D2: depth,
            Dchk1: false,
            Dchk2: false,
            Ddir1: false,
            Ddir2: false,
            Dang1: 1.74532925199433E-02,
            Dang2: 1.74532925199433E-02,
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
        return new
        {
            activeSketchName = TryGetComName(activeSketch),
            depth,
            flipDirection,
            returnedFeature = CaptureFeatureSnapshot(returnedFeature),
            topFeatureBefore,
            topFeatureAfter,
            bodyBefore,
            bodyAfter,
        };
    }

    public object RawCutActiveSketchExplicit(double depth, bool flip, bool dir)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var doc = RequireActiveDoc();
        var activeSketch = doc.GetActiveSketch2() as ISketch
            ?? throw new InvalidOperationException("No active sketch is being edited.");
        var featureManager = _manager.SwApp!.FeatureManager
            ?? throw new InvalidOperationException("No active feature manager available.");

        var topFeatureBefore = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var bodyBefore = CaptureBodySignature(doc);

        var returnedFeature = featureManager.FeatureCut4(
            Sd: true,
            Flip: flip,
            Dir: dir,
            T1: 0,
            T2: 0,
            D1: depth,
            D2: depth,
            Dchk1: false,
            Dchk2: false,
            Ddir1: false,
            Ddir2: false,
            Dang1: 1.74532925199433E-02,
            Dang2: 1.74532925199433E-02,
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
        return new
        {
            activeSketchName = TryGetComName(activeSketch),
            depth,
            flip,
            dir,
            returnedFeature = CaptureFeatureSnapshot(returnedFeature),
            topFeatureBefore,
            topFeatureAfter,
            bodyBefore,
            bodyAfter,
        };
    }

    public object ReplayRecordedVbaFeatureCut(string sketchName)
    {
        if (string.IsNullOrWhiteSpace(sketchName))
        {
            throw new ArgumentException("sketchName is required.", nameof(sketchName));
        }

        var doc = RequireActiveDoc();
        var featureManager = _manager.SwApp!.FeatureManager
            ?? throw new InvalidOperationException("No active feature manager available.");

        bool firstSelect = doc.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, false, 0, null, 0);
        doc.ClearSelection2(true);
        doc.SketchManager?.InsertSketch(true);
        bool secondSelect = doc.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, false, 0, null, 0);

        var topFeatureBefore = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var bodyBefore = CaptureBodySignature(doc);
        var selectionsBefore = DescribeSelections();

        var returnedFeature = featureManager.FeatureCut4(
            true,
            false,
            false,
            0,
            0,
            0.002,
            0.002,
            false,
            false,
            false,
            false,
            1.74532925199433E-02,
            1.74532925199433E-02,
            false,
            false,
            false,
            false,
            false,
            true,
            true,
            true,
            true,
            false,
            0,
            0,
            false,
            false);

        if (doc.SelectionManager is SelectionMgr selectionManager)
        {
            selectionManager.EnableContourSelection = false;
        }

        var topFeatureAfter = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
        var bodyAfter = CaptureBodySignature(doc);
        var selectionsAfter = DescribeSelections();

        return new
        {
            sketchName,
            firstSelect,
            secondSelect,
            topFeatureBefore,
            topFeatureAfter,
            returnedFeature = CaptureFeatureSnapshot(returnedFeature),
            bodyBefore,
            bodyAfter,
            selectionsBefore,
            selectionsAfter,
            isEditing = doc.GetActiveSketch2() != null,
            activeSketchName = TryGetComName(doc.GetActiveSketch2() as ISketch),
        };
    }

    public object RawCutSketchRegionByPoint(double x, double y, double z, double depth, bool flipDirection = false)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var doc = RequireActiveDoc();
        var selectionManager = doc.ISelectionManager
            ?? throw new InvalidOperationException("No active selection manager available.");
        var featureManager = _manager.SwApp!.FeatureManager
            ?? throw new InvalidOperationException("No active feature manager available.");

        selectionManager.EnableContourSelection = true;
        try
        {
            doc.ClearSelection2(true);
            bool selected = doc.Extension.SelectByID2("", "SKETCHREGION", x, y, z, false, 0, null, 0);

            var topFeatureBefore = CaptureFeatureSnapshot(doc.IFeatureByPositionReverse(0));
            var bodyBefore = CaptureBodySignature(doc);
            var selectionsBefore = DescribeSelections();

            var returnedFeature = featureManager.FeatureCut4(
                Sd: true,
                Flip: flipDirection,
                Dir: !flipDirection,
                T1: 0,
                T2: 0,
                D1: depth,
                D2: depth,
                Dchk1: false,
                Dchk2: false,
                Ddir1: false,
                Ddir2: false,
                Dang1: 1.74532925199433E-02,
                Dang2: 1.74532925199433E-02,
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
            return new
            {
                x,
                y,
                z,
                depth,
                flipDirection,
                selected,
                selectionsBefore,
                returnedFeature = CaptureFeatureSnapshot(returnedFeature),
                topFeatureBefore,
                topFeatureAfter,
                bodyBefore,
                bodyAfter,
            };
        }
        finally
        {
            selectionManager.EnableContourSelection = false;
        }
    }

    public object CutRectangleOnFace(int faceIndex, double x1, double y1, double x2, double y2, double depth, bool flipDirection = false)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        if (active.Type != (int)SwDocType.Part)
        {
            throw new InvalidOperationException("rectangle-face-cut requires an active part document.");
        }

        RequireActiveDoc().ClearSelection2(true);

        var hostSelection = _selection.SelectEntity(SelectableEntityType.Face, faceIndex);
        if (!hostSelection.Success)
        {
            throw new InvalidOperationException(hostSelection.Message);
        }

        _sketch.InsertSketch();
        var rectangle = _sketch.AddRectangle(x1, y1, x2, y2);
        var featureManager = _manager.SwApp!.FeatureManager
            ?? throw new InvalidOperationException("No active feature manager available.");
        var feature = featureManager.FeatureCut4(
            Sd: true,
            Flip: flipDirection,
            Dir: !flipDirection,
            T1: (int)EndCondition.Blind,
            T2: 0,
            D1: depth,
            D2: depth,
            Dchk1: false,
            Dchk2: false,
            Ddir1: false,
            Ddir2: false,
            Dang1: 1.74532925199433E-02,
            Dang2: 1.74532925199433E-02,
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
            OptimizeGeometry: false)
            ?? throw new InvalidOperationException("SolidWorks did not create the rectangle cut feature.");

        return new
        {
            active,
            faceIndex,
            x1,
            y1,
            x2,
            y2,
            depth,
            flipDirection,
            rectangle,
            feature,
        };
    }

    public object CutByRayAndConvertEntities(
        double rayPointX,
        double rayPointY,
        double rayPointZ,
        double rayVectorX,
        double rayVectorY,
        double rayVectorZ,
        double radius,
        int selectionType,
        bool append = false,
        int mark = 0,
        int option = 0,
        double depth = 0.002,
        double depth2 = 0.01)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");
        if (active.Type != (int)SwDocType.Part)
        {
            throw new InvalidOperationException("ray-convert-cut requires an active part document.");
        }

        var part = RequireActiveDoc();
        bool selected = part.Extension.SelectByRay(
            rayPointX,
            rayPointY,
            rayPointZ,
            rayVectorX,
            rayVectorY,
            rayVectorZ,
            radius,
            selectionType,
            append,
            mark,
            option);
        if (!selected)
        {
            throw new InvalidOperationException("SolidWorks did not select a face by ray.");
        }

        var sketchManager = part.SketchManager
            ?? throw new InvalidOperationException("No active sketch manager available.");
        sketchManager.InsertSketch(true);

        bool converted = sketchManager.SketchUseEdge3(false, false);
        if (!converted)
        {
            throw new InvalidOperationException("SolidWorks did not convert the selected face into sketch entities.");
        }

        part.ClearSelection2(true);
        sketchManager.InsertSketch(true);

        var featureManager = part.FeatureManager
            ?? throw new InvalidOperationException("No active feature manager available.");
        var feature = featureManager.FeatureCut4(
            Sd: true,
            Flip: false,
            Dir: false,
            T1: 0,
            T2: 0,
            D1: depth,
            D2: depth2,
            Dchk1: false,
            Dchk2: false,
            Ddir1: false,
            Ddir2: false,
            Dang1: 1.74532925199433E-02,
            Dang2: 1.74532925199433E-02,
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
            OptimizeGeometry: false)
            ?? throw new InvalidOperationException("SolidWorks did not create the ray-based cut feature.");

        if (part.SelectionManager is SelectionMgr selectionManager)
        {
            selectionManager.EnableContourSelection = false;
        }

        return new
        {
            active,
            rayPointX,
            rayPointY,
            rayPointZ,
            rayVectorX,
            rayVectorY,
            rayVectorZ,
            radius,
            selectionType,
            depth,
            depth2,
            converted,
            featureName = feature.Name,
            featureType = feature.GetTypeName2(),
        };
    }

    public object DebugRaySelection(
        double rayPointX,
        double rayPointY,
        double rayPointZ,
        double rayVectorX,
        double rayVectorY,
        double rayVectorZ,
        double radius,
        int selectionType,
        bool append = false,
        int mark = 0,
        int option = 0)
    {
        var part = RequireActiveDoc();
        part.ClearSelection2(true);

        bool selected = part.Extension.SelectByRay(
            rayPointX,
            rayPointY,
            rayPointZ,
            rayVectorX,
            rayVectorY,
            rayVectorZ,
            radius,
            selectionType,
            append,
            mark,
            option);

        var selectionManager = part.SelectionManager as SelectionMgr
            ?? throw new InvalidOperationException("No active selection manager available.");
        int selectionCount = selectionManager.GetSelectedObjectCount2(-1);
        int? selectedType = selectionCount > 0 ? selectionManager.GetSelectedObjectType3(1, -1) : null;

        return new
        {
            selected,
            selectionCount,
            selectedType,
            rayPointX,
            rayPointY,
            rayPointZ,
            rayVectorX,
            rayVectorY,
            rayVectorZ,
            radius,
            selectionType,
        };
    }

    public object InspectFaceOutline(int faceIndex, bool chain = true, bool innerLoops = false)
    {
        var part = RequireActiveDoc();
        part.ClearSelection2(true);

        var selection = _selection.SelectEntity(SelectableEntityType.Face, faceIndex);
        if (!selection.Success)
        {
            throw new InvalidOperationException(selection.Message);
        }

        _sketch.InsertSketch();

        var profileSelection = _selection.SelectEntity(SelectableEntityType.Face, faceIndex);
        if (!profileSelection.Success)
        {
            throw new InvalidOperationException(profileSelection.Message);
        }

        var faceEdges = OpenSketchAndSelectFaceEdges(faceIndex);
        _sketch.SketchUseEdge3(chain, innerLoops);

        var activeSketch = part.GetActiveSketch2() as ISketch
            ?? throw new InvalidOperationException("No active sketch after converting the face outline.");
        var segments = ((object[]?)activeSketch.GetSketchSegments() ?? Array.Empty<object>())
            .Select((segment, index) => DescribeSketchSegment(segment, index))
            .ToArray();

        return new
        {
            faceIndex,
            chain,
            innerLoops,
            edgeCount = faceEdges.Length,
            segmentCount = segments.Length,
            segments,
        };
    }

    private static object DescribeSketchSegment(object segment, int index)
    {
        double[]? box = null;
        object[]? points = null;

        try
        {
            box = InvokeComDoubleArray(segment, "GetBox");
            points = DescribeSketchPoints(segment);
        }
        catch
        {
            box = null;
            points = null;
        }

        return new
        {
            index,
            runtimeType = segment.GetType().FullName,
            box,
            points,
        };
    }

    private static bool ShouldRetryOppositeDirection(SolidWorksApiException ex)
        => ex.Message.Contains("did not create a new cut feature", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("did not change the solid body during cut extrude", StringComparison.OrdinalIgnoreCase);

    private static object CaptureFeatureSnapshot(Feature? feature)
    {
        if (feature == null)
        {
            return new { name = (string?)null, typeName = (string?)null };
        }

        return new
        {
            name = feature.Name,
            typeName = feature.GetTypeName2(),
        };
    }

    private static object CaptureBodySignature(IModelDoc2 doc)
    {
        if (doc is not IPartDoc part)
        {
            return new { bodyCount = 0, faceCount = 0, edgeCount = 0, vertexCount = 0 };
        }

        var bodies = (object[]?)part.GetBodies2((int)swBodyType_e.swSolidBody, true) ?? Array.Empty<object>();
        var primaryBody = bodies.OfType<IBody2>().FirstOrDefault();
        if (primaryBody == null)
        {
            return new { bodyCount = bodies.Length, faceCount = 0, edgeCount = 0, vertexCount = 0 };
        }

        int faceCount = ((object[]?)primaryBody.GetFaces() ?? Array.Empty<object>()).Length;
        int edgeCount = ((object[]?)primaryBody.GetEdges() ?? Array.Empty<object>()).Length;
        int vertexCount = ((object[]?)primaryBody.GetVertices() ?? Array.Empty<object>()).Length;
        return new
        {
            bodyCount = bodies.Length,
            faceCount,
            edgeCount,
            vertexCount,
        };
    }

    private static object[]? DescribeSketchPoints(object segment)
    {
        var sketchPoints = InvokeComObjectArray(segment, "GetSketchPoints2");
        if (sketchPoints == null || sketchPoints.Length == 0)
        {
            return null;
        }

        return sketchPoints
            .Select(DescribeSketchPoint)
            .Cast<object>()
            .ToArray();
    }

    private static object DescribeSketchPoint(object point)
    {
        return new
        {
            x = InvokeComDouble(point, "X"),
            y = InvokeComDouble(point, "Y"),
            z = InvokeComDouble(point, "Z"),
        };
    }

    private static double[]? InvokeComDoubleArray(object target, string methodName)
        => ToDoubleArray(target.GetType().InvokeMember(methodName, System.Reflection.BindingFlags.InvokeMethod, null, target, null));

    private static object[]? InvokeComObjectArray(object target, string methodName)
        => target.GetType().InvokeMember(methodName, System.Reflection.BindingFlags.InvokeMethod, null, target, null) as object[];

    private static double InvokeComDouble(object target, string propertyName)
        => Convert.ToDouble(target.GetType().InvokeMember(propertyName, System.Reflection.BindingFlags.GetProperty, null, target, null));

    private static double[]? ToDoubleArray(object? value)
    {
        if (value is not Array array)
        {
            return null;
        }

        return array.Cast<object?>()
            .Where(item => item != null)
            .Select(Convert.ToDouble)
            .ToArray();
    }

    private static IReadOnlyList<object> GetSketchRegions(ISketch sketch)
    {
        var regions = sketch.GetType().InvokeMember(
            "GetSketchRegions",
            System.Reflection.BindingFlags.InvokeMethod,
            binder: null,
            target: sketch,
            args: null) as object[];

        return regions ?? Array.Empty<object>();
    }

    public object ListAssemblyComponents(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("assemblyPath is required.", nameof(assemblyPath));
        }

        ActivateDocument(assemblyPath);
        return new
        {
            assemblyPath,
            active = _documents.GetActiveDocument(),
            components = _assembly.ListComponentsRecursive(),
        };
    }

    public object ActivateExistingDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path is required.", nameof(path));
        }

        ActivateDocument(path);

        return new
        {
            path,
            active = _documents.GetActiveDocument(),
        };
    }

    public object SaveActiveDocument()
    {
        var active = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document.");

        if (string.IsNullOrWhiteSpace(active.Path))
        {
            throw new InvalidOperationException("Active document has no saved path.");
        }

        var result = _documents.SaveDocument(active.Path);
        return new
        {
            active,
            result,
        };
    }

    public object ShowView(SwStandardView view)
    {
        _documents.ShowStandardView(view);
        return new
        {
            view,
            active = _documents.GetActiveDocument(),
        };
    }

    public object MeasureEntities(
        SelectableEntityType firstEntityType,
        int firstIndex,
        string? firstComponentName,
        SelectableEntityType secondEntityType,
        int secondIndex,
        string? secondComponentName,
        int arcOption = 1)
    {
        var result = _selection.MeasureEntities(
            firstEntityType,
            firstIndex,
            secondEntityType,
            secondIndex,
            firstComponentName,
            secondComponentName,
            arcOption);

        return new
        {
            active = _documents.GetActiveDocument(),
            result,
        };
    }

    public object ListEntities(SelectableEntityType? entityType = null, string? componentName = null)
    {
        return new
        {
            active = _documents.GetActiveDocument(),
            entityType = entityType?.ToString(),
            componentName,
            entities = _selection.ListEntities(entityType, componentName),
        };
    }

    public object SketchUseEdge3(bool chain = false, bool innerLoops = true)
    {
        _sketch.SketchUseEdge3(chain, innerLoops);
        return new
        {
            active = _documents.GetActiveDocument(),
            chain,
            innerLoops,
            projected = true,
        };
    }

    public object ExportIsolatedViews(string assemblyPath, IReadOnlyList<string> hierarchyPathsToKeep, string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("assemblyPath is required.", nameof(assemblyPath));
        }

        if (hierarchyPathsToKeep == null || hierarchyPathsToKeep.Count == 0)
        {
            throw new ArgumentException("At least one hierarchy path must be provided.", nameof(hierarchyPathsToKeep));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("outputDirectory is required.", nameof(outputDirectory));
        }

        ActivateDocument(assemblyPath);
        Directory.CreateDirectory(outputDirectory);

        var assembly = RequireActiveAssembly();
        var instances = EnumerateAssemblyComponents(assembly).ToList();
        var keep = new HashSet<string>(hierarchyPathsToKeep, StringComparer.OrdinalIgnoreCase);

        foreach (var instance in instances)
        {
            instance.Component.Visible = keep.Contains(instance.HierarchyPath)
                ? (int)swComponentVisibilityState_e.swComponentVisible
                : (int)swComponentVisibilityState_e.swComponentHidden;
        }

        try
        {
            var exports = new List<object>();
            exports.Add(ExportView(SwStandardView.Front, "front", outputDirectory));
            exports.Add(ExportView(SwStandardView.Top, "top", outputDirectory));
            exports.Add(ExportView(SwStandardView.Right, "right", outputDirectory));

            return new
            {
                assemblyPath,
                active = _documents.GetActiveDocument(),
                keptHierarchyPaths = hierarchyPathsToKeep,
                outputDirectory,
                exports,
            };
        }
        finally
        {
            foreach (var instance in instances)
            {
                instance.Component.Visible = (int)swComponentVisibilityState_e.swComponentVisible;
            }
        }
    }

    public void Dispose()
    {
        _manager.Disconnect();
    }

    private object PreparePlanarMate(MatePreparationKind kind)
    {
        string partPath = CreateBoxPartFile();
        var assemblyInfo = _documents.NewDocument(SwDocType.Assembly);
        var componentA = _assembly.InsertComponent(partPath, 0, 0, 0);
        var componentB = _assembly.InsertComponent(partPath, 0.08, 0, 0);

        var doc = RequireActiveDoc();
        string assemblyTitle = doc.GetTitle();
        string planeA = kind switch
        {
            MatePreparationKind.Distance => GetRightPlaneName(),
            _ => GetFrontPlaneName(),
        };
        string planeB = kind switch
        {
            MatePreparationKind.Angle => GetRightPlaneName(),
            MatePreparationKind.Distance => GetRightPlaneName(),
            _ => GetFrontPlaneName(),
        };

        SelectAssemblyPlane(doc, assemblyTitle, componentA.Name, planeA, append: false, mark: 0);
        SelectAssemblyPlane(doc, assemblyTitle, componentB.Name, planeB, append: true, mark: 0);
        EnsureSelectionCountAtLeast(2, $"{kind} mate setup");

        return new
        {
            assembly = assemblyInfo,
            componentA = componentA.Name,
            componentB = componentB.Name,
            mateType = kind.ToString(),
            selectionCount = GetSelectionCount(),
            selectionDetails = DescribeSelections(),
        };
    }

    private object PrepareConcentricMate()
    {
        string partPath = CreateCylinderPartFile();
        var assemblyInfo = _documents.NewDocument(SwDocType.Assembly);
        var componentA = _assembly.InsertComponent(partPath, 0, 0, 0);
        var componentB = _assembly.InsertComponent(partPath, 0.03, 0, 0);

        var assemblyDoc = RequireActiveAssembly();
        var components = ((object[]?)assemblyDoc.GetComponents(true) ?? Array.Empty<object>())
            .OfType<IComponent2>()
            .ToArray();

        if (components.Length < 2)
        {
            throw new InvalidOperationException("Expected at least two components in the active assembly.");
        }

        SelectFirstCylindricalFace(components[0], append: false, mark: 0);
        SelectFirstCylindricalFace(components[1], append: true, mark: 0);
        EnsureSelectionCountAtLeast(2, "concentric mate setup");

        return new
        {
            assembly = assemblyInfo,
            componentA = componentA.Name,
            componentB = componentB.Name,
            mateType = MatePreparationKind.Concentric.ToString(),
            selectionCount = GetSelectionCount(),
            selectionDetails = DescribeSelections(),
        };
    }

    private string CreateBoxPartFile()
    {
        ResetSession();
        CreateBoxPart(0.02, 0.02, 0.01);
        string path = Path.Combine(Path.GetTempPath(), $"SwMcpMateBox_{Guid.NewGuid():N}.sldprt");
        SaveActiveDocumentAs(path);
        return path;
    }

    private string CreateCylinderPartFile()
    {
        ResetSession();
        _documents.NewDocument(SwDocType.Part);
        SelectFrontPlane();
        _sketch.InsertSketch();
        _sketch.AddCircle(0, 0, 0.01);
        var feature = _feature.Extrude(0.02);

        string path = Path.Combine(Path.GetTempPath(), $"SwMcpMateCylinder_{Guid.NewGuid():N}.sldprt");
        SaveActiveDocumentAs(path);
        return path;
    }

    private void CreateBoxPart(double width, double height, double depth)
    {
        _documents.NewDocument(SwDocType.Part);
        SelectFrontPlane();
        _sketch.InsertSketch();
        _sketch.AddRectangle(-width / 2, -height / 2, width / 2, height / 2);
        _feature.Extrude(depth);
    }

    private void SelectFrontPlane()
    {
        var plane = GetDefaultPlaneByIndex(0);
        var result = _selection.SelectByName(plane.SelectionName, plane.SelectionType);
        if (!result.Success)
        {
            throw new InvalidOperationException("Unable to select the front plane.");
        }
    }

    private bool TrySelectByNames(IEnumerable<string> names, string selType)
    {
        foreach (var name in names)
        {
            var result = _selection.SelectByName(name, selType);
            if (result.Success)
            {
                return true;
            }
        }

        return false;
    }

    private string GetFrontPlaneName() => GetDefaultPlaneByIndex(0).SelectionName;

    private string GetTopPlaneName() => GetDefaultPlaneByIndex(1).SelectionName;

    private string GetRightPlaneName() => GetDefaultPlaneByIndex(2).SelectionName;

    private ReferencePlaneInfo GetDefaultPlaneByIndex(int index)
    {
        var planes = _selection.ListReferencePlanes();
        if (planes.Count <= index)
        {
            throw new InvalidOperationException($"Expected at least {index + 1} reference planes in the active document, but found {planes.Count}.");
        }

        return planes[index];
    }

    private void SelectFirstEdge()
    {
        _selection.ClearSelection();
        var edge = ((object[]?)GetPrimaryBody().GetEdges() ?? Array.Empty<object>())
            .OfType<IEdge>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No edge found on the active solid body.");
        SelectEntity((IEntity)edge, append: false);
    }

    private void SelectAllEdges()
    {
        _selection.ClearSelection();
        var edges = ((object[]?)GetPrimaryBody().GetEdges() ?? Array.Empty<object>())
            .OfType<IEdge>()
            .ToArray();

        if (edges.Length == 0)
        {
            throw new InvalidOperationException("No edges found on the active solid body.");
        }

        for (int index = 0; index < edges.Length; index++)
        {
            SelectEntity((IEntity)edges[index], append: index > 0);
        }
    }

    private void SelectFirstPlanarFace()
    {
        _selection.ClearSelection();
        var face = FindFirstPlanarFace()
            ?? throw new InvalidOperationException("No planar face found on the active solid body.");
        SelectEntity((IEntity)face, append: false);
    }

    private void SelectTopPlanarFace()
    {
        _selection.ClearSelection();
        var face = FindTopPlanarFace()
            ?? throw new InvalidOperationException("No top planar face found on the active solid body.");
        SelectEntity((IEntity)face, append: false);
    }

    private IFace2? FindFirstPlanarFace()
    {
        return ((object[]?)GetPrimaryBody().GetFaces() ?? Array.Empty<object>())
            .OfType<IFace2>()
            .FirstOrDefault(face =>
            {
                var surface = face.GetSurface() as ISurface;
                return surface != null && surface.IsPlane();
            });
    }

    private IFace2? FindTopPlanarFace()
    {
        return ((object[]?)GetPrimaryBody().GetFaces() ?? Array.Empty<object>())
            .OfType<IFace2>()
            .Select(face => new
            {
                Face = face,
                Surface = face.GetSurface() as ISurface,
                Box = face.GetBox() as double[],
            })
            .Where(candidate => candidate.Surface != null && candidate.Surface.IsPlane())
            .Where(candidate => candidate.Box != null && candidate.Box.Length >= 6)
            .OrderByDescending(candidate => candidate.Box![5])
            .Select(candidate => candidate.Face)
            .FirstOrDefault();
    }

    private void SelectFirstCylindricalFace(IComponent2 component, bool append, int mark = 1)
    {
        var body = GetPrimaryBody(component);
        var face = ((object[]?)body.GetFaces() ?? Array.Empty<object>())
            .OfType<IFace2>()
            .FirstOrDefault(face =>
            {
                var surface = face.GetSurface() as ISurface;
                return surface != null && surface.IsCylinder();
            })
            ?? throw new InvalidOperationException($"No cylindrical face found for component {component.Name2}.");

        SelectEntity((IEntity)face, append, mark);
    }

    private void SelectAssemblyPlane(IModelDoc2 doc, string assemblyTitle, string componentName, string planeName, bool append, int mark = 1)
    {
        bool selected = doc.Extension.SelectByID2(
            $"{planeName}@{componentName}@{assemblyTitle}",
            "PLANE",
            0,
            0,
            0,
            append,
            mark,
            null,
            0);

        if (!selected)
        {
            throw new InvalidOperationException(
                $"Failed to select assembly plane {planeName}@{componentName}@{assemblyTitle}.");
        }
    }

    private void SelectEntity(IEntity entity, bool append, int mark = 1)
    {
        var selectData = CreateSelectData();
        selectData.Mark = mark;
        if (!entity.Select4(append, selectData))
        {
            throw new InvalidOperationException("Failed to select SolidWorks entity.");
        }
    }

    private void SelectComObject(object comObject, bool append, int mark = 1)
    {
        var selectData = CreateSelectData();
        selectData.Mark = mark;
        var result = comObject.GetType().InvokeMember(
            "Select4",
            System.Reflection.BindingFlags.InvokeMethod,
            binder: null,
            target: comObject,
            args: [append, selectData]);

        bool selected = result switch
        {
            bool boolResult => boolResult,
            int intResult => intResult != 0,
            _ => false,
        };

        if (!selected)
        {
            throw new InvalidOperationException("Failed to select SolidWorks COM object.");
        }
    }

    private int GetSelectionCount()
    {
        var selectionManager = RequireActiveDoc().ISelectionManager
            ?? throw new InvalidOperationException("No selection manager available.");
        return selectionManager.GetSelectedObjectCount2(-1);
    }

    private SelectData CreateSelectData()
    {
        var selectionManager = RequireActiveDoc().ISelectionManager
            ?? throw new InvalidOperationException("No selection manager available.");
        return (SelectData)selectionManager.CreateSelectData();
    }

    private void EnsureSelectionCountAtLeast(int expectedCount, string context)
    {
        int actualCount = GetSelectionCount();
        if (actualCount < expectedCount)
        {
            throw new InvalidOperationException(
                $"Expected at least {expectedCount} selected entities for {context}, but found {actualCount}.");
        }
    }

    private object[] DescribeSelections()
    {
        var selectionManager = RequireActiveDoc().ISelectionManager
            ?? throw new InvalidOperationException("No selection manager available.");
        int count = selectionManager.GetSelectedObjectCount2(-1);
        var selections = new List<object>(count);

        for (int index = 1; index <= count; index++)
        {
            var selected = selectionManager.GetSelectedObject6(index, -1);
            selections.Add(new
            {
                index,
                typeCode = selectionManager.GetSelectedObjectType3(index, -1),
                runtimeType = selected?.GetType().FullName,
                name = TryGetComName(selected),
            });
        }

        return selections.ToArray();
    }

    private static string? TryGetComName(object? selected)
    {
        if (selected == null)
        {
            return null;
        }

        return selected switch
        {
            IFeature feature => feature.Name,
            IComponent2 component => component.Name2,
            ISketch => TryInvokeComString(selected, "GetName") ?? selected.GetType().Name,
            IEntity => selected.GetType().Name,
            _ => selected.GetType().Name,
        };
    }

    private static string? TryInvokeComString(object target, string methodName)
    {
        try
        {
            return target.GetType().InvokeMember(
                methodName,
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                target: target,
                args: null) as string;
        }
        catch
        {
            return null;
        }
    }

    private IBody2 GetPrimaryBody()
    {
        var part = RequireActivePart();
        return GetPrimaryBody(part);
    }

    private static IBody2 GetPrimaryBody(IPartDoc part)
    {
        var bodies = (object[]?)part.GetBodies2((int)swBodyType_e.swSolidBody, true)
            ?? throw new InvalidOperationException("No solid body found in the active part.");
        return bodies.OfType<IBody2>().FirstOrDefault()
            ?? throw new InvalidOperationException("No solid body found in the active part.");
    }

    private static IBody2 GetPrimaryBody(IComponent2 component)
    {
        var bodies = component.GetBodies3((int)swBodyType_e.swSolidBody, out _ ) as object[]
            ?? throw new InvalidOperationException($"No body found for component {component.Name2}.");
        return bodies.OfType<IBody2>().FirstOrDefault()
            ?? throw new InvalidOperationException($"No body found for component {component.Name2}.");
    }

    private object ExportView(SwStandardView view, string fileStem, string outputDirectory)
    {
        _documents.ShowStandardView(view);
        RequireActiveDoc().ViewZoomtofit2();
        string outputPath = Path.Combine(outputDirectory, $"{fileStem}.png");
        var export = _documents.ExportCurrentViewPng(outputPath, 1600, 900, false);
        return new
        {
            view = view.ToString(),
            outputPath,
            export.Width,
            export.Height,
            export.MimeType,
        };
    }

    private static IEnumerable<AssemblyComponentInstance> EnumerateAssemblyComponents(IAssemblyDoc assembly)
    {
        var roots = (object[]?)assembly.GetComponents(true) ?? Array.Empty<object>();
        foreach (var component in roots.OfType<IComponent2>())
        {
            foreach (var instance in TraverseComponent(component, component.Name2 ?? "Component"))
            {
                yield return instance;
            }
        }
    }

    private static IEnumerable<AssemblyComponentInstance> TraverseComponent(IComponent2 component, string hierarchyPath)
    {
        yield return new AssemblyComponentInstance(component, hierarchyPath);

        var children = (object[]?)component.GetChildren() ?? Array.Empty<object>();
        foreach (var child in children.OfType<IComponent2>())
        {
            string childName = child.Name2 ?? "Component";
            foreach (var instance in TraverseComponent(child, $"{hierarchyPath}/{childName}"))
            {
                yield return instance;
            }
        }
    }

    private static bool TopLevelFeatureExists(IModelDoc2 doc, string featureName)
    {
        for (var feature = doc.FirstFeature() as Feature; feature != null; feature = feature.GetNextFeature() as Feature)
        {
            if (string.Equals(feature.Name, featureName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private object[] OpenSketchAndSelectFaceEdges(int faceIndex)
    {
        var part = RequireActiveDoc();
        part.ClearSelection2(true);

        var faceSelection = _selection.SelectEntity(SelectableEntityType.Face, faceIndex);
        if (!faceSelection.Success)
        {
            throw new InvalidOperationException(faceSelection.Message);
        }

        var selectedFace = part.ISelectionManager?.GetSelectedObject6(1, -1) as IFace2
            ?? throw new InvalidOperationException("Failed to resolve the selected face before opening sketch mode.");

        _sketch.InsertSketch();
        return SelectEdgesForSketchUseEdge3(part, selectedFace);
    }

    private object[] SelectEdgesForSketchUseEdge3(IModelDoc2 part, IFace2 face)
    {
        var faceEdges = ((object[]?)face.GetEdges() ?? Array.Empty<object>())
            .ToArray();
        if (faceEdges.Length == 0)
        {
            throw new InvalidOperationException("The selected face does not expose any edges.");
        }

        part.ClearSelection2(true);
        for (int index = 0; index < faceEdges.Length; index++)
        {
            SelectComObject(faceEdges[index], append: index > 0, mark: 0);
        }

        return faceEdges;
    }

    private IModelDoc2 RequireActiveDoc()
        => _manager.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException("No active document.");

    private void ActivateDocument(string path)
    {
        _documents.OpenDocument(path);
        _ = _manager.SwApp!.ActivateDoc(path);
    }

    private IPartDoc RequireActivePart()
        => RequireActiveDoc() as IPartDoc
            ?? throw new InvalidOperationException("Active document is not a part.");

    private IAssemblyDoc RequireActiveAssembly()
        => RequireActiveDoc() as IAssemblyDoc
            ?? throw new InvalidOperationException("Active document is not an assembly.");

    private void SaveActiveDocumentAs(string path)
    {
        var doc = RequireActiveDoc();
        _ = doc.SaveAs3(path, 0, 0);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"SaveAs3 did not produce the expected file: {path}.");
        }
    }

    private object ActiveDocumentInfo()
    {
        var info = _documents.GetActiveDocument()
            ?? throw new InvalidOperationException("Expected an active document after setup.");
        return new { title = info.Title, path = info.Path, type = info.Type };
    }
}