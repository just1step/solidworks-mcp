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
            _ => throw new ArgumentException($"Unknown command: {args[0]}")
        };
    }
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
            IEntity => selected.GetType().Name,
            _ => selected.GetType().Name,
        };
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

    private IModelDoc2 RequireActiveDoc()
        => _manager.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException("No active document.");

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