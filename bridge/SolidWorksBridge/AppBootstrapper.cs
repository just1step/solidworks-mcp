using System.Text.Json;
using SolidWorksBridge.Models;
using SolidWorksBridge.PipeServer;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge;

/// <summary>
/// Wires all application dependencies and registers pipe method handlers.
/// Extracted from Program.cs to make the wiring logic unit-testable without
/// starting a real pipe server or connecting to SolidWorks.
/// </summary>
public class AppBootstrapper
{
    private readonly ISwConnectionManager _connectionManager;
    private readonly IDocumentService _documentService;
    private readonly ISelectionService _selectionService;
    private readonly ISketchService _sketchService;
    private readonly IFeatureService _featureService;
    private readonly IAssemblyService _assemblyService;
    private readonly IWorkflowService _workflowService;
    private readonly MessageHandler _messageHandler;

    public AppBootstrapper(
        ISwConnectionManager connectionManager,
        IDocumentService documentService,
        ISelectionService selectionService,
        ISketchService sketchService,
        IFeatureService featureService,
        IAssemblyService assemblyService,
        IWorkflowService workflowService,
        MessageHandler messageHandler)
    {
        _connectionManager = connectionManager
            ?? throw new ArgumentNullException(nameof(connectionManager));
        _documentService = documentService
            ?? throw new ArgumentNullException(nameof(documentService));
        _selectionService = selectionService
            ?? throw new ArgumentNullException(nameof(selectionService));
        _sketchService = sketchService
            ?? throw new ArgumentNullException(nameof(sketchService));
        _featureService = featureService
            ?? throw new ArgumentNullException(nameof(featureService));
        _assemblyService = assemblyService
            ?? throw new ArgumentNullException(nameof(assemblyService));
        _workflowService = workflowService
            ?? throw new ArgumentNullException(nameof(workflowService));
        _messageHandler = messageHandler
            ?? throw new ArgumentNullException(nameof(messageHandler));
    }

    /// <summary>
    /// Register all pipe method handlers onto the MessageHandler.
    /// Called once at startup before the pipe server starts accepting connections.
    /// </summary>
    public void RegisterHandlers()
    {
        // ── Connection ────────────────────────────────────────────
        _messageHandler.Register("sw.connect", _ =>
        {
            _connectionManager.Connect();
            return Task.FromResult<object?>(new { connected = true });
        });

        _messageHandler.Register("sw.disconnect", _ =>
        {
            _connectionManager.Disconnect();
            return Task.FromResult<object?>(new { connected = false });
        });

        _messageHandler.Register("sw.get_runtime_compatibility", _ =>
        {
            var result = _connectionManager.GetCompatibilityInfo();
            return Task.FromResult<object?>(result);
        });

        // ── Document lifecycle ────────────────────────────────────
        _messageHandler.Register("sw.new_document", req =>
        {
            var p = req.GetParams<NewDocumentParams>()
                ?? throw new ArgumentException("params required: {type, templatePath?}");

            var docType = ParseDocType(p.Type);
            var doc = _documentService.NewDocument(docType, p.TemplatePath);
            return Task.FromResult<object?>(doc);
        });

        _messageHandler.Register("sw.open_document", req =>
        {
            var p = req.GetParams<PathParams>()
                ?? throw new ArgumentException("params required: {path}");

            var doc = _documentService.OpenDocument(p.Path);
            return Task.FromResult<object?>(doc);
        });

        _messageHandler.Register("sw.close_document", req =>
        {
            var p = req.GetParams<PathParams>()
                ?? throw new ArgumentException("params required: {path}");

            _documentService.CloseDocument(p.Path);
            return Task.FromResult<object?>(new { closed = true });
        });

        _messageHandler.Register("sw.save_document", req =>
        {
            var p = req.GetParams<PathParams>()
                ?? throw new ArgumentException("params required: {path}");

            var result = _documentService.SaveDocument(p.Path);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.save_document_as", req =>
        {
            var p = req.GetParams<SaveDocumentAsParams>()
                ?? throw new ArgumentException("params required: {outputPath, sourcePath?, saveAsCopy?}");

            var result = _documentService.SaveDocumentAs(p.OutputPath, p.SourcePath, p.SaveAsCopy);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.undo", req =>
        {
            var p = req.GetParams<UndoParams>() ?? new UndoParams();
            _documentService.Undo(p.Steps);
            return Task.FromResult<object?>(new { undone = true, steps = p.Steps });
        });

        _messageHandler.Register("sw.list_documents", _ =>
        {
            var docs = _documentService.ListDocuments();
            return Task.FromResult<object?>(docs);
        });

        _messageHandler.Register("sw.get_active_document", _ =>
        {
            var doc = _documentService.GetActiveDocument();
            return Task.FromResult<object?>(doc);
        });

        _messageHandler.Register("sw.view.show_standard", req =>
        {
            var p = req.GetParams<ShowStandardViewParams>() ?? new ShowStandardViewParams();
            _documentService.ShowStandardView(ParseStandardView(p.View));
            return Task.FromResult<object?>(new { changed = true, view = p.View });
        });

        _messageHandler.Register("sw.view.rotate", req =>
        {
            var p = req.GetParams<RotateViewParams>() ?? new RotateViewParams();
            _documentService.RotateView(p.XDegrees, p.YDegrees, p.ZDegrees);
            return Task.FromResult<object?>(new { rotated = true, xDegrees = p.XDegrees, yDegrees = p.YDegrees, zDegrees = p.ZDegrees });
        });

        _messageHandler.Register("sw.view.export_png", req =>
        {
            var p = req.GetParams<ExportViewPngParams>()
                ?? throw new ArgumentException("params required: {outputPath, width?, height?, includeBase64Data?}");
            var result = _documentService.ExportCurrentViewPng(p.OutputPath, p.Width, p.Height, p.IncludeBase64Data);
            return Task.FromResult<object?>(result);
        });

        // ── Selection ─────────────────────────────────────────────
        _messageHandler.Register("sw.select.by_name", req =>
        {
            var p = req.GetParams<SelectByNameParams>()
                ?? throw new ArgumentException("params required: {name, selType}");
            var result = _selectionService.SelectByName(p.Name, p.SelType);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.select.list_entities", req =>
        {
            var p = req.GetParams<ListEntitiesParams>() ?? new ListEntitiesParams();
            var result = _selectionService.ListEntities(p.EntityType, p.ComponentName);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.select.list_reference_planes", _ =>
        {
            var result = _selectionService.ListReferencePlanes();
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.select.get_edit_state", _ =>
        {
            var result = _selectionService.GetEditState();
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.select.get_feature_diagnostics", _ =>
        {
            var result = _selectionService.GetFeatureDiagnostics();
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.select.entity", req =>
        {
            var p = req.GetParams<SelectEntityParams>()
                ?? throw new ArgumentException("params required: {entityType, index, append?, mark?, componentName?}");
            var result = _selectionService.SelectEntity(p.EntityType, p.Index, p.Append, p.Mark, p.ComponentName);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.select.measure_entities", req =>
        {
            var p = req.GetParams<MeasureEntitiesParams>()
                ?? throw new ArgumentException("params required: {firstEntityType, firstIndex, secondEntityType, secondIndex, firstComponentName?, secondComponentName?, arcOption?}");
            var result = _selectionService.MeasureEntities(
                p.FirstEntityType,
                p.FirstIndex,
                p.SecondEntityType,
                p.SecondIndex,
                p.FirstComponentName,
                p.SecondComponentName,
                p.ArcOption);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.select.clear", _ =>
        {
            _selectionService.ClearSelection();
            return Task.FromResult<object?>(new { cleared = true });
        });

        // ── Sketch ────────────────────────────────────────────────
        _messageHandler.Register("sw.sketch.insert", _ =>
        {
            _sketchService.InsertSketch();
            return Task.FromResult<object?>(new { editing = true });
        });

        _messageHandler.Register("sw.sketch.finish", _ =>
        {
            _sketchService.FinishSketch();
            return Task.FromResult<object?>(new { editing = false });
        });

        _messageHandler.Register("sw.sketch.add_point", req =>
        {
            var p = req.GetParams<AddPointParams>()
                ?? throw new ArgumentException("params required: {x,y}");
            var info = _sketchService.AddPoint(p.X, p.Y);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.sketch.add_ellipse", req =>
        {
            var p = req.GetParams<AddEllipseParams>()
                ?? throw new ArgumentException("params required: {cx,cy,majorX,majorY,minorX,minorY}");
            var info = _sketchService.AddEllipse(p.Cx, p.Cy, p.MajorX, p.MajorY, p.MinorX, p.MinorY);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.sketch.add_polygon", req =>
        {
            var p = req.GetParams<AddPolygonParams>()
                ?? throw new ArgumentException("params required: {cx,cy,x,y,sides,inscribed}");
            var info = _sketchService.AddPolygon(p.Cx, p.Cy, p.X, p.Y, p.Sides, p.Inscribed);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.sketch.add_text", req =>
        {
            var p = req.GetParams<AddTextParams>()
                ?? throw new ArgumentException("params required: {x,y,text}");
            var info = _sketchService.AddText(p.X, p.Y, p.Text);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.sketch.add_line", req =>
        {
            var p = req.GetParams<AddLineParams>()
                ?? throw new ArgumentException("params required: {x1,y1,x2,y2}");
            var info = _sketchService.AddLine(p.X1, p.Y1, p.X2, p.Y2);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.sketch.add_circle", req =>
        {
            var p = req.GetParams<AddCircleParams>()
                ?? throw new ArgumentException("params required: {cx,cy,radius}");
            var info = _sketchService.AddCircle(p.Cx, p.Cy, p.Radius);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.sketch.add_rectangle", req =>
        {
            var p = req.GetParams<AddRectangleParams>()
                ?? throw new ArgumentException("params required: {x1,y1,x2,y2}");
            var info = _sketchService.AddRectangle(p.X1, p.Y1, p.X2, p.Y2);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.sketch.add_arc", req =>
        {
            var p = req.GetParams<AddArcParams>()
                ?? throw new ArgumentException("params required: {cx,cy,x1,y1,x2,y2,direction}");
            var info = _sketchService.AddArc(p.Cx, p.Cy, p.X1, p.Y1, p.X2, p.Y2, p.Direction);
            return Task.FromResult<object?>(info);
        });

        // ── Feature ───────────────────────────────────────────────
        _messageHandler.Register("sw.feature.extrude", req =>
        {
            var p = req.GetParams<ExtrudeParams>()
                ?? throw new ArgumentException("params required: {depth, endCondition?, flipDirection?}");
            var info = _featureService.Extrude(p.Depth, p.EndCondition, p.FlipDirection);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.feature.extrude_cut", req =>
        {
            var p = req.GetParams<ExtrudeParams>()
                ?? throw new ArgumentException("params required: {depth, endCondition?, flipDirection?}");
            var info = _featureService.ExtrudeCut(p.Depth, p.EndCondition, p.FlipDirection);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.feature.revolve", req =>
        {
            var p = req.GetParams<RevolveParams>()
                ?? throw new ArgumentException("params required: {angleDegrees, isCut?}");
            var info = _featureService.Revolve(p.AngleDegrees, p.IsCut);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.feature.fillet", req =>
        {
            var p = req.GetParams<RadiusParams>()
                ?? throw new ArgumentException("params required: {radius}");
            var info = _featureService.Fillet(p.Radius);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.feature.chamfer", req =>
        {
            var p = req.GetParams<ChamferParams>()
                ?? throw new ArgumentException("params required: {distance}");
            var info = _featureService.Chamfer(p.Distance);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.feature.shell", req =>
        {
            var p = req.GetParams<ShellParams>()
                ?? throw new ArgumentException("params required: {thickness}");
            var info = _featureService.Shell(p.Thickness);
            return Task.FromResult<object?>(info);
        });

        // ── Assembly ──────────────────────────────────────────────
        _messageHandler.Register("sw.assembly.insert_component", req =>
        {
            var p = req.GetParams<InsertComponentParams>()
                ?? throw new ArgumentException("params required: {filePath, x?, y?, z?}");
            var info = _assemblyService.InsertComponent(p.FilePath, p.X, p.Y, p.Z);
            return Task.FromResult<object?>(info);
        });

        _messageHandler.Register("sw.assembly.add_mate_coincident", req =>
        {
            var p = req.GetParams<MateAlignParams>() ?? new MateAlignParams();
            var result = _assemblyService.AddMateCoincident(p.Align);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.assembly.add_mate_concentric", req =>
        {
            var p = req.GetParams<MateAlignParams>() ?? new MateAlignParams();
            var result = _assemblyService.AddMateConcentric(p.Align);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.assembly.add_mate_parallel", req =>
        {
            var p = req.GetParams<MateAlignParams>() ?? new MateAlignParams();
            var result = _assemblyService.AddMateParallel(p.Align);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.assembly.add_mate_distance", req =>
        {
            var p = req.GetParams<MateDistanceParams>()
                ?? throw new ArgumentException("params required: {distance}");
            var result = _assemblyService.AddMateDistance(p.Distance, p.Align);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.assembly.add_mate_angle", req =>
        {
            var p = req.GetParams<MateAngleParams>()
                ?? throw new ArgumentException("params required: {angleDegrees}");
            var result = _assemblyService.AddMateAngle(p.AngleDegrees, p.Align);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.assembly.list_components", _ =>
        {
            var list = _assemblyService.ListComponents();
            return Task.FromResult<object?>(list);
        });

        _messageHandler.Register("sw.assembly.list_components_recursive", _ =>
        {
            var list = _assemblyService.ListComponentsRecursive();
            return Task.FromResult<object?>(list);
        });

        _messageHandler.Register("sw.assembly.resolve_component_target", req =>
        {
            var p = req.GetParams<AssemblyResolveComponentTargetParams>() ?? new AssemblyResolveComponentTargetParams();
            var result = _assemblyService.ResolveComponentTarget(p.ComponentName, p.HierarchyPath, p.ComponentPath);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.assembly.analyze_shared_part_edit_impact", req =>
        {
            var p = req.GetParams<AssemblyResolveComponentTargetParams>() ?? new AssemblyResolveComponentTargetParams();
            var result = _assemblyService.AnalyzeSharedPartEditImpact(p.ComponentName, p.HierarchyPath, p.ComponentPath);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.assembly.check_interference", req =>
        {
            var p = req.GetParams<AssemblyInterferenceParams>() ?? new AssemblyInterferenceParams();
            var result = _assemblyService.CheckInterference(p.HierarchyPaths, p.TreatCoincidenceAsInterference);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.assembly.replace_component", req =>
        {
            var p = req.GetParams<AssemblyReplaceComponentParams>()
                ?? throw new ArgumentException("params required: {hierarchyPath, replacementFilePath, configName?, replaceAllInstances?, useConfigChoice?, reattachMates?}");
            var result = _assemblyService.ReplaceComponent(
                p.HierarchyPath,
                p.ReplacementFilePath,
                p.ConfigName,
                p.ReplaceAllInstances,
                p.UseConfigChoice,
                p.ReattachMates);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.workflow.replace_nested_component_and_verify_persistence", req =>
        {
            var p = req.GetParams<ReplaceNestedComponentAndVerifyPersistenceParams>()
                ?? throw new ArgumentException("params required: {replacementFilePath, componentName?, hierarchyPath?, componentPath?, configName?, useConfigChoice?, reattachMates?}");
            var result = _workflowService.ReplaceNestedComponentAndVerifyPersistence(
                p.ReplacementFilePath,
                p.ComponentName,
                p.HierarchyPath,
                p.ComponentPath,
                p.ConfigName,
                p.UseConfigChoice,
                p.ReattachMates);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.workflow.diagnose_active_document_health", req =>
        {
            var p = req.GetParams<DiagnoseActiveDocumentHealthParams>() ?? new DiagnoseActiveDocumentHealthParams();
            var result = _workflowService.DiagnoseActiveDocumentHealth(p.ForceRebuild, p.TopOnly, p.SaveDocument);
            return Task.FromResult<object?>(result);
        });

        _messageHandler.Register("sw.workflow.review_targeted_static_interference", req =>
        {
            var p = req.GetParams<ReviewTargetedStaticInterferenceParams>()
                ?? throw new ArgumentException("params required: {firstHierarchyPath, secondHierarchyPath, treatCoincidenceAsInterference?}");
            var result = _workflowService.ReviewTargetedStaticInterference(
                p.FirstHierarchyPath,
                p.SecondHierarchyPath,
                p.TreatCoincidenceAsInterference);
            return Task.FromResult<object?>(result);
        });

    }

    // ── Param DTOs ────────────────────────────────────────────────

    public class NewDocumentParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string Type { get; set; } = "Part";

        [System.Text.Json.Serialization.JsonPropertyName("templatePath")]
        public string? TemplatePath { get; set; }
    }

    public class PathParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
    }

    public class SaveDocumentAsParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("outputPath")]
        public string OutputPath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("sourcePath")]
        public string? SourcePath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("saveAsCopy")]
        public bool SaveAsCopy { get; set; } = true;
    }

    public class UndoParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("steps")]
        public int Steps { get; set; } = 1;
    }

    public class ShowStandardViewParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("view")]
        public string View { get; set; } = "isometric";
    }

    public class RotateViewParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("xDegrees")]
        public double XDegrees { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("yDegrees")]
        public double YDegrees { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("zDegrees")]
        public double ZDegrees { get; set; }
    }

    public class ExportViewPngParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("outputPath")]
        public string OutputPath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("width")]
        public int Width { get; set; } = 1600;

        [System.Text.Json.Serialization.JsonPropertyName("height")]
        public int Height { get; set; } = 900;

        [System.Text.Json.Serialization.JsonPropertyName("includeBase64Data")]
        public bool IncludeBase64Data { get; set; }
    }

    public class SelectByNameParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("selType")]
        public string SelType { get; set; } = string.Empty;
    }

    public class ListEntitiesParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("entityType")]
        public SelectableEntityType? EntityType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("componentName")]
        public string? ComponentName { get; set; }
    }

    public class SelectEntityParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("entityType")]
        public SelectableEntityType EntityType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("index")]
        public int Index { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("append")]
        public bool Append { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("mark")]
        public int Mark { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("componentName")]
        public string? ComponentName { get; set; }
    }

    public class MeasureEntitiesParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("firstEntityType")]
        public SelectableEntityType FirstEntityType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("firstIndex")]
        public int FirstIndex { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("secondEntityType")]
        public SelectableEntityType SecondEntityType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("secondIndex")]
        public int SecondIndex { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("firstComponentName")]
        public string? FirstComponentName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("secondComponentName")]
        public string? SecondComponentName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("arcOption")]
        public int ArcOption { get; set; } = 1;
    }

    public class AddLineParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("x1")] public double X1 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y1")] public double Y1 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("x2")] public double X2 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y2")] public double Y2 { get; set; }
    }

    public class AddPointParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("x")] public double X { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y")] public double Y { get; set; }
    }

    public class AddEllipseParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("cx")] public double Cx { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("cy")] public double Cy { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("majorX")] public double MajorX { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("majorY")] public double MajorY { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("minorX")] public double MinorX { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("minorY")] public double MinorY { get; set; }
    }

    public class AddPolygonParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("cx")] public double Cx { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("cy")] public double Cy { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("x")] public double X { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y")] public double Y { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("sides")] public int Sides { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("inscribed")] public bool Inscribed { get; set; } = true;
    }

    public class AddTextParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("x")] public double X { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y")] public double Y { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    public class AddCircleParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("cx")] public double Cx { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("cy")] public double Cy { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("radius")] public double Radius { get; set; }
    }

    public class AddRectangleParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("x1")] public double X1 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y1")] public double Y1 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("x2")] public double X2 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y2")] public double Y2 { get; set; }
    }

    public class AddArcParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("cx")] public double Cx { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("cy")] public double Cy { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("x1")] public double X1 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y1")] public double Y1 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("x2")] public double X2 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y2")] public double Y2 { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("direction")] public int Direction { get; set; } = 1;
    }

    public class ExtrudeParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("depth")] public double Depth { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("endCondition")] public EndCondition EndCondition { get; set; } = EndCondition.Blind;
        [System.Text.Json.Serialization.JsonPropertyName("flipDirection")] public bool FlipDirection { get; set; }
    }

    public class RevolveParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("angleDegrees")] public double AngleDegrees { get; set; } = 360;
        [System.Text.Json.Serialization.JsonPropertyName("isCut")] public bool IsCut { get; set; }
    }

    public class RadiusParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("radius")] public double Radius { get; set; }
    }

    public class ChamferParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("distance")] public double Distance { get; set; }
    }

    public class ShellParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("thickness")] public double Thickness { get; set; }
    }

    public class InsertComponentParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("filePath")] public string FilePath { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("x")] public double X { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("y")] public double Y { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("z")] public double Z { get; set; }
    }

    public class MateAlignParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("align")] public MateAlign Align { get; set; } = MateAlign.Closest;
    }

    public class MateDistanceParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("distance")] public double Distance { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("align")] public MateAlign Align { get; set; } = MateAlign.Closest;
    }

    public class MateAngleParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("angleDegrees")] public double AngleDegrees { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("align")] public MateAlign Align { get; set; } = MateAlign.Closest;
    }

    public class AssemblyInterferenceParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("hierarchyPaths")]
        public string[]? HierarchyPaths { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("treatCoincidenceAsInterference")]
        public bool TreatCoincidenceAsInterference { get; set; }
    }

    public class AssemblyResolveComponentTargetParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("componentName")]
        public string? ComponentName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("hierarchyPath")]
        public string? HierarchyPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("componentPath")]
        public string? ComponentPath { get; set; }
    }

    public class AssemblyReplaceComponentParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("hierarchyPath")]
        public string HierarchyPath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("replacementFilePath")]
        public string ReplacementFilePath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("configName")]
        public string ConfigName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("replaceAllInstances")]
        public bool ReplaceAllInstances { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("useConfigChoice")]
        public int UseConfigChoice { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reattachMates")]
        public bool ReattachMates { get; set; } = true;
    }

    public class ReplaceNestedComponentAndVerifyPersistenceParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("replacementFilePath")]
        public string ReplacementFilePath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("componentName")]
        public string? ComponentName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("hierarchyPath")]
        public string? HierarchyPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("componentPath")]
        public string? ComponentPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("configName")]
        public string ConfigName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("useConfigChoice")]
        public int UseConfigChoice { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reattachMates")]
        public bool ReattachMates { get; set; } = true;
    }

    public class DiagnoseActiveDocumentHealthParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("forceRebuild")]
        public bool ForceRebuild { get; set; } = true;

        [System.Text.Json.Serialization.JsonPropertyName("topOnly")]
        public bool TopOnly { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("saveDocument")]
        public bool SaveDocument { get; set; }
    }

    public class ReviewTargetedStaticInterferenceParams
    {
        [System.Text.Json.Serialization.JsonPropertyName("firstHierarchyPath")]
        public string FirstHierarchyPath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("secondHierarchyPath")]
        public string SecondHierarchyPath { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("treatCoincidenceAsInterference")]
        public bool TreatCoincidenceAsInterference { get; set; }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static SwDocType ParseDocType(string type) =>
        type.ToLowerInvariant() switch
        {
            "part" or "1" => SwDocType.Part,
            "assembly" or "2" => SwDocType.Assembly,
            "drawing" or "3" => SwDocType.Drawing,
            _ => throw new ArgumentException($"Unknown document type: '{type}'. Use Part, Assembly, or Drawing.")
        };

    private static SwStandardView ParseStandardView(string view) =>
        view.ToLowerInvariant() switch
        {
            "front" => SwStandardView.Front,
            "back" => SwStandardView.Back,
            "left" => SwStandardView.Left,
            "right" => SwStandardView.Right,
            "top" => SwStandardView.Top,
            "bottom" => SwStandardView.Bottom,
            "iso" or "isometric" => SwStandardView.Isometric,
            "trimetric" => SwStandardView.Trimetric,
            "dimetric" => SwStandardView.Dimetric,
            _ => throw new ArgumentException($"Unknown standard view: '{view}'.")
        };

    /// <summary>
    /// Factory: create a production AppBootstrapper with real implementations.
    /// </summary>
    public static AppBootstrapper CreateProduction()
    {
        var connector = new SwComConnector();
        var connectionManager = new SwConnectionManager(connector);
        var documentService = new DocumentService(connectionManager);
        var selectionService = new SelectionService(connectionManager);
        var sketchService = new SketchService(connectionManager);
        var featureService = new FeatureService(connectionManager);
        var assemblyService = new AssemblyService(connectionManager);
        var workflowService = new WorkflowService(documentService, assemblyService, selectionService, connectionManager);
        var messageHandler = new MessageHandler();

        return new AppBootstrapper(
            connectionManager, documentService,
            selectionService, sketchService, featureService, assemblyService, workflowService,
            messageHandler);
    }

    /// <summary>Expose the wired MessageHandler for use by PipeServerManager.</summary>
    public MessageHandler MessageHandler => _messageHandler;
}
