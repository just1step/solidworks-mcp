using System.Text.Json;
using Moq;
using SolidWorksBridge.Models;
using SolidWorksBridge.PipeServer;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests;

public class AppBootstrapperTests
{
    // ── Helpers ───────────────────────────────────────────────────

    private static (AppBootstrapper bootstrapper,
                    Mock<ISwConnectionManager> manager,
                    Mock<IDocumentService> docSvc,
                    MessageHandler handler)
        Build()
    {
        var manager = new Mock<ISwConnectionManager>();
        var docSvc = new Mock<IDocumentService>();
        var selSvc = new Mock<ISelectionService>();
        var sketchSvc = new Mock<ISketchService>();
        var featureSvc = new Mock<IFeatureService>();
        var assemblySvc = new Mock<IAssemblyService>();
        var workflowSvc = new Mock<IWorkflowService>();
        var handler = new MessageHandler();
        var bootstrapper = new AppBootstrapper(
            manager.Object, docSvc.Object,
            selSvc.Object, sketchSvc.Object, featureSvc.Object, assemblySvc.Object, workflowSvc.Object,
            handler);
        return (bootstrapper, manager, docSvc, handler);
    }

    private static (AppBootstrapper bootstrapper,
                    Mock<ISelectionService> selSvc,
                    MessageHandler handler)
        BuildWithSelection()
    {
        var manager = new Mock<ISwConnectionManager>();
        var docSvc = new Mock<IDocumentService>();
        var selSvc = new Mock<ISelectionService>();
        var sketchSvc = new Mock<ISketchService>();
        var featureSvc = new Mock<IFeatureService>();
        var assemblySvc = new Mock<IAssemblyService>();
        var workflowSvc = new Mock<IWorkflowService>();
        var handler = new MessageHandler();
        var bootstrapper = new AppBootstrapper(
            manager.Object, docSvc.Object,
            selSvc.Object, sketchSvc.Object, featureSvc.Object, assemblySvc.Object, workflowSvc.Object,
            handler);
        return (bootstrapper, selSvc, handler);
    }

    private static (AppBootstrapper bootstrapper,
                    Mock<IWorkflowService> workflowSvc,
                    MessageHandler handler)
        BuildWithWorkflow()
    {
        var manager = new Mock<ISwConnectionManager>();
        var docSvc = new Mock<IDocumentService>();
        var selSvc = new Mock<ISelectionService>();
        var sketchSvc = new Mock<ISketchService>();
        var featureSvc = new Mock<IFeatureService>();
        var assemblySvc = new Mock<IAssemblyService>();
        var workflowSvc = new Mock<IWorkflowService>();
        var handler = new MessageHandler();
        var bootstrapper = new AppBootstrapper(
            manager.Object, docSvc.Object,
            selSvc.Object, sketchSvc.Object, featureSvc.Object, assemblySvc.Object, workflowSvc.Object,
            handler);
        return (bootstrapper, workflowSvc, handler);
    }

    /// Build a PipeRequest with JSON params.
    private static PipeRequest Req(string method, object? @params = null)
    {
        var json = @params == null ? "{}" : JsonSerializer.Serialize(@params);
        return new PipeRequest
        {
            Id = "test-1",
            Method = method,
            Params = JsonDocument.Parse(json).RootElement
        };
    }

    private static SwOpenResult OpenedDoc(string path, int type = 1) =>
        new(new SwDocumentInfo(path, Path.GetFileNameWithoutExtension(path), type),
            new SwApiDiagnostics(0, Array.Empty<SwCodeInfo>(), 0, Array.Empty<SwCodeInfo>()));

    private static SolidWorksCompatibilityInfo CompatibilityInfo(string state = "certified-baseline") =>
        new(
            state,
            "compatibility summary",
            "32.1.0",
            32,
            2024,
            new SolidWorksRuntimeVersionInfo(
                "32.0.0",
                32,
                0,
                0,
                2024,
                new SwBuildNumbers("32", "32.0.0", string.Empty),
                @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\sldworks.exe"),
            new SolidWorksLicenseInfo(0, "swLicenseType_Full", "Full SolidWorks license."),
            new[] { "notice" });

    // ─────────────────────────────────────────────────────────────
    // Registration
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void RegisterHandlers_RegistersAllMethods()
    {
        var (bootstrapper, _, _, handler) = Build();
        bootstrapper.RegisterHandlers();

        var expected = new[]
        {
            "sw.connect", "sw.disconnect", "sw.get_runtime_compatibility",
            "sw.new_document", "sw.open_document",
            "sw.close_document", "sw.save_document", "sw.save_document_as", "sw.undo",
            "sw.list_documents", "sw.get_active_document",
            "sw.view.show_standard", "sw.view.rotate", "sw.view.export_png",
            "sw.select.by_name", "sw.select.list_entities",
            "sw.select.entity", "sw.select.measure_entities", "sw.select.clear", "sw.select.get_feature_diagnostics",
            "sw.sketch.insert", "sw.sketch.finish",
            "sw.sketch.add_line", "sw.sketch.add_circle",
            "sw.sketch.add_rectangle", "sw.sketch.add_arc",
            "sw.feature.extrude", "sw.feature.extrude_cut",
            "sw.feature.revolve", "sw.feature.fillet",
            "sw.feature.chamfer", "sw.feature.shell",
            "sw.assembly.insert_component",
            "sw.assembly.add_mate_coincident", "sw.assembly.add_mate_concentric",
            "sw.assembly.add_mate_parallel",
            "sw.assembly.add_mate_distance", "sw.assembly.add_mate_angle",
            "sw.assembly.list_components", "sw.assembly.list_components_recursive",
            "sw.assembly.check_interference", "sw.assembly.replace_component",
            "sw.workflow.diagnose_active_document_health",
            "sw.workflow.replace_nested_component_and_verify_persistence",
            "sw.workflow.review_targeted_static_interference"
        };

        foreach (var method in expected)
            Assert.True(handler.HasMethod(method), $"Method '{method}' not registered");
    }

    // ─────────────────────────────────────────────────────────────
    // sw.connect / sw.disconnect
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_SwConnect_CallsConnect_ReturnsConnectedTrue()
    {
        var (bootstrapper, manager, _, handler) = Build();
        bootstrapper.RegisterHandlers();

        var response = await handler.HandleAsync(Req("sw.connect"));

        Assert.True(response.Error == null, response.Error?.Message);
        manager.Verify(m => m.Connect(), Times.Once);
    }

    [Fact]
    public async Task Handler_SwConnect_IncludesCompatibilityContextWhenAvailable()
    {
        var (bootstrapper, manager, _, handler) = Build();
        bootstrapper.RegisterHandlers();
        manager.Setup(m => m.GetCompatibilityInfo()).Returns(CompatibilityInfo("planned-next-version"));

        var response = await handler.HandleAsync(Req("sw.connect"));

        Assert.Null(response.Error);
        manager.Verify(m => m.Connect(), Times.Once);
        manager.Verify(m => m.GetCompatibilityInfo(), Times.Once);

        string json = JsonSerializer.Serialize(response.Result, PipeMessageSerializer.Options);
        Assert.Contains("\"connected\":true", json);
        Assert.Contains("\"compatibilityState\":\"planned-next-version\"", json);
        Assert.Contains("\"compatibilityAdvisory\"", json);
    }

    [Fact]
    public async Task Handler_SwDisconnect_CallsDisconnect_ReturnsConnectedFalse()
    {
        var (bootstrapper, manager, _, handler) = Build();
        bootstrapper.RegisterHandlers();

        var response = await handler.HandleAsync(Req("sw.disconnect"));

        Assert.True(response.Error == null, response.Error?.Message);
        manager.Verify(m => m.Disconnect(), Times.Once);
    }

    [Fact]
    public async Task Handler_SwGetRuntimeCompatibility_CallsManager()
    {
        var (bootstrapper, manager, _, handler) = Build();
        bootstrapper.RegisterHandlers();
        manager.Setup(m => m.GetCompatibilityInfo()).Returns(CompatibilityInfo());

        var response = await handler.HandleAsync(Req("sw.get_runtime_compatibility"));

        Assert.True(response.Error == null, response.Error?.Message);
        manager.Verify(m => m.GetCompatibilityInfo(), Times.Once);

        string json = JsonSerializer.Serialize(response.Result, PipeMessageSerializer.Options);
        Assert.Contains("\"compatibilityState\":\"certified-baseline\"", json);
    }

    // ─────────────────────────────────────────────────────────────
    // sw.new_document
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_SwNewDocument_Part_CallsServiceWithPartType()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        var expected = new SwDocumentInfo(@"C:\u.sldprt", "u", 1);
        docSvc.Setup(d => d.NewDocument(SwDocType.Part, null)).Returns(expected);

        var response = await handler.HandleAsync(Req("sw.new_document", new { type = "Part" }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.NewDocument(SwDocType.Part, null), Times.Once);
    }

    [Fact]
    public async Task Handler_SwNewDocument_Assembly_CallsServiceWithAssemblyType()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        var expected = new SwDocumentInfo(@"C:\u.sldasm", "u", 2);
        docSvc.Setup(d => d.NewDocument(SwDocType.Assembly, null)).Returns(expected);

        var response = await handler.HandleAsync(Req("sw.new_document", new { type = "Assembly" }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.NewDocument(SwDocType.Assembly, null), Times.Once);
    }

    [Fact]
    public async Task Handler_SwNewDocument_WithTemplate_PassesTemplatePath()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        const string tpl = @"C:\tpl\part.prtdot";
        var expected = new SwDocumentInfo(@"C:\u.sldprt", "u", 1);
        docSvc.Setup(d => d.NewDocument(SwDocType.Part, tpl)).Returns(expected);

        var response = await handler.HandleAsync(
            Req("sw.new_document", new { type = "Part", templatePath = tpl }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.NewDocument(SwDocType.Part, tpl), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // sw.open_document
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_SwOpenDocument_CallsServiceWithPath()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        const string path = @"C:\model.sldprt";
        var expected = OpenedDoc(path);
        docSvc.Setup(d => d.OpenDocument(path)).Returns(expected);

        var response = await handler.HandleAsync(Req("sw.open_document", new { path }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.OpenDocument(path), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // sw.close_document
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_SwCloseDocument_CallsServiceWithPath()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        const string path = @"C:\model.sldprt";

        var response = await handler.HandleAsync(Req("sw.close_document", new { path }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.CloseDocument(path), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // sw.save_document
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_SwSaveDocument_CallsServiceWithPath()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        const string path = @"C:\model.sldprt";

        var response = await handler.HandleAsync(Req("sw.save_document", new { path }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.SaveDocument(path), Times.Once);
    }

    [Fact]
    public async Task Handler_SwSaveDocumentAs_CallsServiceWithParams()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        const string sourcePath = @"C:\model.sldprt";
        const string outputPath = @"C:\exports\model.step";
        var expected = new SwSaveResult(sourcePath, outputPath, "step", true, 0, 0);
        docSvc.Setup(d => d.SaveDocumentAs(outputPath, sourcePath, true)).Returns(expected);

        var response = await handler.HandleAsync(Req("sw.save_document_as", new { outputPath, sourcePath, saveAsCopy = true }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.SaveDocumentAs(outputPath, sourcePath, true), Times.Once);
    }

    [Fact]
    public async Task Handler_SwUndo_CallsServiceWithSteps()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();

        var response = await handler.HandleAsync(Req("sw.undo", new { steps = 2 }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.Undo(2), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // sw.list_documents
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_SwListDocuments_ReturnsDocumentArray()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        var docs = new[]
        {
            new SwDocumentInfo(@"C:\a.sldprt", "a", 1),
            new SwDocumentInfo(@"C:\b.sldasm", "b", 2),
        };
        docSvc.Setup(d => d.ListDocuments()).Returns(docs);

        var response = await handler.HandleAsync(Req("sw.list_documents"));

        Assert.Null(response.Error);
        Assert.NotNull(response.Result);
        docSvc.Verify(d => d.ListDocuments(), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // sw.get_active_document
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_SwGetActiveDocument_ReturnsActiveDoc()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        var active = new SwDocumentInfo(@"C:\active.sldprt", "active", 1);
        docSvc.Setup(d => d.GetActiveDocument()).Returns(active);

        var response = await handler.HandleAsync(Req("sw.get_active_document"));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.GetActiveDocument(), Times.Once);
    }

    [Fact]
    public async Task Handler_SwGetActiveDocument_WhenNone_ReturnsSuccessWithNullResult()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        docSvc.Setup(d => d.GetActiveDocument()).Returns((SwDocumentInfo?)null);

        var response = await handler.HandleAsync(Req("sw.get_active_document"));

        // Should succeed (no error), result is null — that's valid
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task Handler_SwSelectGetFeatureDiagnostics_CallsSelectionService()
    {
        var manager = new Mock<ISwConnectionManager>();
        var docSvc = new Mock<IDocumentService>();
        var selSvc = new Mock<ISelectionService>();
        var sketchSvc = new Mock<ISketchService>();
        var featureSvc = new Mock<IFeatureService>();
        var assemblySvc = new Mock<IAssemblyService>();
        var workflowSvc = new Mock<IWorkflowService>();
        var handler = new MessageHandler();
        var bootstrapper = new AppBootstrapper(
            manager.Object, docSvc.Object,
            selSvc.Object, sketchSvc.Object, featureSvc.Object, assemblySvc.Object, workflowSvc.Object,
            handler);
        bootstrapper.RegisterHandlers();

        selSvc.Setup(s => s.GetFeatureDiagnostics()).Returns(new FeatureDiagnosticsResult(
            new[]
            {
                new FeatureDiagnosticInfo(0, "MateCoincident1", "MateCoincident", false, false, true, 46, false, "swFeatureErrorMateOverdefined", "Mate overdefined.", true),
            },
            new[]
            {
                new WhatsWrongItemInfo("MateCoincident1", "MateCoincident", 46, false, "swFeatureErrorMateOverdefined", "Mate overdefined."),
            },
            1,
            0));

        var response = await handler.HandleAsync(Req("sw.select.get_feature_diagnostics"));

        Assert.Null(response.Error);
        selSvc.Verify(s => s.GetFeatureDiagnostics(), Times.Once);
    }

    [Fact]
    public async Task Handler_DiagnoseActiveDocumentHealth_CallsWorkflowService()
    {
        var (bootstrapper, workflowSvc, handler) = BuildWithWorkflow();
        bootstrapper.RegisterHandlers();

        workflowSvc.Setup(w => w.DiagnoseActiveDocumentHealth(true, false, true)).Returns(new ActiveDocumentHealthDiagnosticsResult(
            new SwDocumentInfo(@"C:\Asm.sldasm", "Asm", 2),
            new EditStateInfo(false, "None", true, true),
            new FeatureDiagnosticsResult(Array.Empty<FeatureDiagnosticInfo>(), Array.Empty<WhatsWrongItemInfo>(), 0, 0),
            new RebuildExecutionResult(true, true, false,
                new RebuildStateInfo(1, true, [new SwCodeInfo(1, "swModelRebuildStatus_NonFrozenFeatureNeedsRebuild", "Non-frozen features need rebuild.")], "Non-frozen features need rebuild."),
                new RebuildStateInfo(0, false, [new SwCodeInfo(0, "swModelRebuildStatus_FullyRebuilt", "The model does not currently need rebuild.")], "The model does not currently need rebuild.")),
            new FeatureDiagnosticsResult(Array.Empty<FeatureDiagnosticInfo>(), Array.Empty<WhatsWrongItemInfo>(), 0, 0),
            new SaveHealthInfo(true, true, @"C:\Asm.sldasm", new SwSaveResult(@"C:\Asm.sldasm", @"C:\Asm.sldasm", "sldasm", false, 0, 0), new SwApiDiagnostics(0, Array.Empty<SwCodeInfo>(), 0, Array.Empty<SwCodeInfo>()), false, false, null),
            false,
            false,
            true,
            "completed",
            null));

        var response = await handler.HandleAsync(Req("sw.workflow.diagnose_active_document_health", new { forceRebuild = true, topOnly = false, saveDocument = true }));

        Assert.Null(response.Error);
        workflowSvc.Verify(w => w.DiagnoseActiveDocumentHealth(true, false, true), Times.Once);
    }

    [Fact]
    public async Task Handler_ReplaceNestedComponentAndVerifyPersistence_CallsWorkflowService()
    {
        var (bootstrapper, workflowSvc, handler) = BuildWithWorkflow();
        bootstrapper.RegisterHandlers();

        const string hierarchyPath = "SubAsm-1/Pulley-1";
        const string replacementFilePath = @"C:\NewPulley.sldprt";
        var resolution = new AssemblyTargetResolutionResult(
            RequestedName: null,
            RequestedHierarchyPath: hierarchyPath,
            RequestedComponentPath: null,
            IsResolved: true,
            IsAmbiguous: false,
            ResolvedInstance: new ComponentInstanceInfo("Pulley-1", @"C:\OldPulley.sldprt", hierarchyPath, 1),
            OwningAssemblyHierarchyPath: "SubAsm-1",
            OwningAssemblyFilePath: @"C:\SubAsm.sldasm",
            SourceFileReuseCount: 2,
            MatchingInstances: new[] { new ComponentInstanceInfo("Pulley-1", @"C:\OldPulley.sldprt", hierarchyPath, 1) });
        var impact = new SharedPartEditImpactResult(
            resolution,
            @"C:\OldPulley.sldprt",
            2,
            new[]
            {
                new ComponentInstanceInfo("Pulley-1", @"C:\OldPulley.sldprt", hierarchyPath, 1),
                new ComponentInstanceInfo("Pulley-2", @"C:\OldPulley.sldprt", "SubAsm-1/Pulley-2", 1),
            },
            false,
            "replace_single_instance_before_edit");
        var expected = new NestedComponentReplacementWorkflowResult(
            resolution,
            impact,
            @"C:\Top.sldasm",
            "SubAsm-1",
            @"C:\SubAsm.sldasm",
            "Pulley-1",
            replacementFilePath,
            true,
            new AssemblyComponentReplacementResult("Pulley-1", replacementFilePath, string.Empty, false, 0, true, true),
            new SwSaveResult(@"C:\SubAsm.sldasm", @"C:\SubAsm.sldasm", "sldasm", false, 0, 0),
            true,
            resolution with { ResolvedInstance = new ComponentInstanceInfo("Pulley-1", replacementFilePath, hierarchyPath, 1) },
            impact with
            {
                SourceFilePath = replacementFilePath,
                AffectedInstanceCount = 1,
                AffectedInstances = new[] { new ComponentInstanceInfo("Pulley-1", replacementFilePath, hierarchyPath, 1) },
                SafeDirectEdit = true,
                RecommendedAction = "safe_direct_edit"
            },
            true,
            "completed",
            null);
        workflowSvc.Setup(w => w.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, null, hierarchyPath, null, "", 0, true))
            .Returns(expected);

        var response = await handler.HandleAsync(Req("sw.workflow.replace_nested_component_and_verify_persistence", new { replacementFilePath, hierarchyPath }));

        Assert.Null(response.Error);
        workflowSvc.Verify(w => w.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, null, hierarchyPath, null, "", 0, true), Times.Once);
    }

    [Fact]
    public async Task Handler_ReviewTargetedStaticInterference_CallsWorkflowService()
    {
        var (bootstrapper, workflowSvc, handler) = BuildWithWorkflow();
        bootstrapper.RegisterHandlers();

        const string firstHierarchyPath = "SubAsm-1/Pulley-1";
        const string secondHierarchyPath = "SubAsm-1/Bracket-1";
        var firstResolution = new AssemblyTargetResolutionResult(
            RequestedName: null,
            RequestedHierarchyPath: firstHierarchyPath,
            RequestedComponentPath: null,
            IsResolved: true,
            IsAmbiguous: false,
            ResolvedInstance: new ComponentInstanceInfo("Pulley-1", @"C:\OldPulley.sldprt", firstHierarchyPath, 1),
            OwningAssemblyHierarchyPath: "SubAsm-1",
            OwningAssemblyFilePath: @"C:\SubAsm.sldasm",
            SourceFileReuseCount: 1,
            MatchingInstances: new[] { new ComponentInstanceInfo("Pulley-1", @"C:\OldPulley.sldprt", firstHierarchyPath, 1) });
        var secondResolution = new AssemblyTargetResolutionResult(
            RequestedName: null,
            RequestedHierarchyPath: secondHierarchyPath,
            RequestedComponentPath: null,
            IsResolved: true,
            IsAmbiguous: false,
            ResolvedInstance: new ComponentInstanceInfo("Bracket-1", @"C:\Bracket.sldprt", secondHierarchyPath, 1),
            OwningAssemblyHierarchyPath: "SubAsm-1",
            OwningAssemblyFilePath: @"C:\SubAsm.sldasm",
            SourceFileReuseCount: 1,
            MatchingInstances: new[] { new ComponentInstanceInfo("Bracket-1", @"C:\Bracket.sldprt", secondHierarchyPath, 1) });
        var interference = new AssemblyInterferenceCheckResult(
            true,
            false,
            2,
            2,
            new[]
            {
                new ComponentInstanceInfo("Pulley-1", @"C:\OldPulley.sldprt", firstHierarchyPath, 1),
                new ComponentInstanceInfo("Bracket-1", @"C:\Bracket.sldprt", secondHierarchyPath, 1),
            });
        workflowSvc.Setup(w => w.ReviewTargetedStaticInterference(firstHierarchyPath, secondHierarchyPath, false))
            .Returns(new TargetedStaticInterferenceReviewResult(
                new[] { firstHierarchyPath, secondHierarchyPath },
                false,
                firstResolution,
                secondResolution,
                new[] { firstHierarchyPath, secondHierarchyPath },
                interference,
                true,
                true,
                true,
                "completed",
                null));

        var response = await handler.HandleAsync(Req("sw.workflow.review_targeted_static_interference", new { firstHierarchyPath, secondHierarchyPath }));

        Assert.Null(response.Error);
        workflowSvc.Verify(w => w.ReviewTargetedStaticInterference(firstHierarchyPath, secondHierarchyPath, false), Times.Once);
    }

    [Fact]
    public async Task Handler_SwViewShowStandard_CallsService()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();

        var response = await handler.HandleAsync(Req("sw.view.show_standard", new { view = "top" }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.ShowStandardView(SwStandardView.Top), Times.Once);
    }

    [Fact]
    public async Task Handler_SwViewRotate_CallsService()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();

        var response = await handler.HandleAsync(Req("sw.view.rotate", new { xDegrees = 15.0, yDegrees = -10.0, zDegrees = 90.0 }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.RotateView(15.0, -10.0, 90.0), Times.Once);
    }

    [Fact]
    public async Task Handler_SwViewExportPng_CallsService()
    {
        var (bootstrapper, _, docSvc, handler) = Build();
        bootstrapper.RegisterHandlers();
        const string outputPath = @"C:\exports\view.png";
        var expected = new SwImageExportResult(outputPath, "image/png", 1024, 768, null);
        docSvc.Setup(d => d.ExportCurrentViewPng(outputPath, 1024, 768, false)).Returns(expected);

        var response = await handler.HandleAsync(Req("sw.view.export_png", new { outputPath, width = 1024, height = 768, includeBase64Data = false }));

        Assert.Null(response.Error);
        docSvc.Verify(d => d.ExportCurrentViewPng(outputPath, 1024, 768, false), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────
    // sw.select.list_entities / sw.select.entity
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_SwSelectListEntities_PassesFiltersToSelectionService()
    {
        var (bootstrapper, selSvc, handler) = BuildWithSelection();
        bootstrapper.RegisterHandlers();
        var entities = new[]
        {
            new SelectableEntityInfo(0, SelectableEntityType.Edge, "Part1-1", [0d, 0d, 0d, 0.01d, 0d, 0d]),
        };
        selSvc.Setup(s => s.ListEntities(SelectableEntityType.Edge, "Part1-1")).Returns(entities);

        var response = await handler.HandleAsync(Req("sw.select.list_entities", new { entityType = "Edge", componentName = "Part1-1" }));

        Assert.Null(response.Error);
        selSvc.Verify(s => s.ListEntities(SelectableEntityType.Edge, "Part1-1"), Times.Once);
    }

    [Fact]
    public async Task Handler_SwSelectEntity_PassesIndexedSelectionParams()
    {
        var (bootstrapper, selSvc, handler) = BuildWithSelection();
        bootstrapper.RegisterHandlers();
        selSvc.Setup(s => s.SelectEntity(SelectableEntityType.Face, 2, true, 1, "Part1-2"))
            .Returns(new SelectionResult(true, "Selected Face at index 2"));

        var response = await handler.HandleAsync(Req("sw.select.entity", new
        {
            entityType = "Face",
            index = 2,
            append = true,
            mark = 1,
            componentName = "Part1-2",
        }));

        Assert.Null(response.Error);
        selSvc.Verify(s => s.SelectEntity(SelectableEntityType.Face, 2, true, 1, "Part1-2"), Times.Once);
    }

    [Fact]
    public async Task Handler_SwSelectMeasureEntities_PassesMeasurementParams()
    {
        var (bootstrapper, selSvc, handler) = BuildWithSelection();
        bootstrapper.RegisterHandlers();
        var expected = new EntityMeasurementResult(
            new MeasuredEntityInfo(SelectableEntityType.Face, 3, "2020铝板-1", [0d, 0d, 0.01d, 0.02d, 0.03d, 0.01d]),
            new MeasuredEntityInfo(SelectableEntityType.Face, 7, "y轴皮带固定滑轮-1", [0d, 0d, 0.0095d, 0.02d, 0.03d, 0.0095d]),
            1,
            0.0005d,
            0.0005d,
            null,
            null,
            0d,
            0d,
            0.0005d,
            null,
            null,
            null,
            null,
            true,
            false,
            false);
        selSvc.Setup(s => s.MeasureEntities(
                SelectableEntityType.Face,
                3,
                SelectableEntityType.Face,
                7,
                "2020铝板-1",
                "y轴皮带固定滑轮-1",
                1))
            .Returns(expected);

        var response = await handler.HandleAsync(Req("sw.select.measure_entities", new
        {
            firstEntityType = "Face",
            firstIndex = 3,
            secondEntityType = "Face",
            secondIndex = 7,
            firstComponentName = "2020铝板-1",
            secondComponentName = "y轴皮带固定滑轮-1",
            arcOption = 1,
        }));

        Assert.Null(response.Error);
        selSvc.Verify(s => s.MeasureEntities(
            SelectableEntityType.Face,
            3,
            SelectableEntityType.Face,
            7,
            "2020铝板-1",
            "y轴皮带固定滑轮-1",
            1), Times.Once);
    }
}
