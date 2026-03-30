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
        var handler = new MessageHandler();
        var bootstrapper = new AppBootstrapper(
            manager.Object, docSvc.Object,
            selSvc.Object, sketchSvc.Object, featureSvc.Object, assemblySvc.Object,
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
        var handler = new MessageHandler();
        var bootstrapper = new AppBootstrapper(
            manager.Object, docSvc.Object,
            selSvc.Object, sketchSvc.Object, featureSvc.Object, assemblySvc.Object,
            handler);
        return (bootstrapper, selSvc, handler);
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
            "sw.connect", "sw.disconnect",
            "sw.new_document", "sw.open_document",
            "sw.close_document", "sw.save_document",
            "sw.list_documents", "sw.get_active_document",
            "sw.select.by_name", "sw.select.list_entities",
            "sw.select.entity", "sw.select.clear",
            "sw.sketch.insert", "sw.sketch.finish",
            "sw.sketch.add_line", "sw.sketch.add_circle",
            "sw.sketch.add_rectangle", "sw.sketch.add_arc",
            "sw.feature.extrude", "sw.feature.extrude_cut",
            "sw.feature.revolve", "sw.feature.fillet",
            "sw.feature.chamfer", "sw.feature.shell", "sw.feature.simple_hole",
            "sw.assembly.insert_component",
            "sw.assembly.add_mate_coincident", "sw.assembly.add_mate_concentric",
            "sw.assembly.add_mate_parallel",
            "sw.assembly.add_mate_distance", "sw.assembly.add_mate_angle",
            "sw.assembly.list_components"
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
    public async Task Handler_SwDisconnect_CallsDisconnect_ReturnsConnectedFalse()
    {
        var (bootstrapper, manager, _, handler) = Build();
        bootstrapper.RegisterHandlers();

        var response = await handler.HandleAsync(Req("sw.disconnect"));

        Assert.True(response.Error == null, response.Error?.Message);
        manager.Verify(m => m.Disconnect(), Times.Once);
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
        var expected = new SwDocumentInfo(path, "model", 1);
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
}
