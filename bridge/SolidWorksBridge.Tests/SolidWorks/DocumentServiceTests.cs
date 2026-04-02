using Moq;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

[Collection("SolidWorks Integration")]
public class DocumentServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Build a mock ISwConnectionManager whose SwApp returns the given ISldWorksApp mock.
    /// EnsureConnected() does nothing (simulates connected state).
    /// </summary>
    private static (Mock<ISwConnectionManager> manager, Mock<ISldWorksApp> swApp) ConnectedMocks()
    {
        var swApp = new Mock<ISldWorksApp>();
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.IsConnected).Returns(true);
        manager.Setup(m => m.SwApp).Returns(swApp.Object);
        manager.Setup(m => m.EnsureConnected()); // no-op
        return (manager, swApp);
    }

    private static SwDocumentInfo FakeDoc(string path = @"C:\model.sldprt", int type = 1) =>
        new(path, System.IO.Path.GetFileNameWithoutExtension(path), type);

    private static SwOpenResult FakeOpenResult(string path = @"C:\model.sldprt", int type = 1) =>
        new(FakeDoc(path, type), new SwApiDiagnostics(0, Array.Empty<SwCodeInfo>(), 0, Array.Empty<SwCodeInfo>()));

    // ─────────────────────────────────────────────────────────────
    // NewDocument
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void NewDocument_Part_UsesDefaultPartTemplate()
    {
        var (manager, swApp) = ConnectedMocks();
        const string template = @"C:\templates\part.prtdot";
        var expected = FakeDoc(@"C:\untitled.sldprt", type: 1);
        swApp.Setup(s => s.GetDefaultTemplatePath(SwDocType.Part)).Returns(template);
        swApp.Setup(s => s.NewDoc(template)).Returns(expected);

        var svc = new DocumentService(manager.Object);
        var result = svc.NewDocument(SwDocType.Part);

        Assert.Equal(expected, result);
        swApp.Verify(s => s.GetDefaultTemplatePath(SwDocType.Part), Times.Once);
        swApp.Verify(s => s.NewDoc(template), Times.Once);
    }

    [Fact]
    public void NewDocument_Assembly_UsesDefaultAssemblyTemplate()
    {
        var (manager, swApp) = ConnectedMocks();
        const string template = @"C:\templates\asm.asmdot";
        var expected = FakeDoc(@"C:\untitled.sldasm", type: 2);
        swApp.Setup(s => s.GetDefaultTemplatePath(SwDocType.Assembly)).Returns(template);
        swApp.Setup(s => s.NewDoc(template)).Returns(expected);

        var svc = new DocumentService(manager.Object);
        var result = svc.NewDocument(SwDocType.Assembly);

        Assert.Equal(expected, result);
        swApp.Verify(s => s.GetDefaultTemplatePath(SwDocType.Assembly), Times.Once);
    }

    [Fact]
    public void NewDocument_Drawing_UsesDefaultDrawingTemplate()
    {
        var (manager, swApp) = ConnectedMocks();
        const string template = @"C:\templates\draw.drwdot";
        var expected = FakeDoc(@"C:\untitled.slddrw", type: 3);
        swApp.Setup(s => s.GetDefaultTemplatePath(SwDocType.Drawing)).Returns(template);
        swApp.Setup(s => s.NewDoc(template)).Returns(expected);

        var svc = new DocumentService(manager.Object);
        var result = svc.NewDocument(SwDocType.Drawing);

        Assert.Equal(expected, result);
        swApp.Verify(s => s.GetDefaultTemplatePath(SwDocType.Drawing), Times.Once);
    }

    [Fact]
    public void NewDocument_CustomTemplate_UsesProvidedPath_SkipsDefaultLookup()
    {
        var (manager, swApp) = ConnectedMocks();
        const string custom = @"C:\custom\my.prtdot";
        var expected = FakeDoc();
        swApp.Setup(s => s.NewDoc(custom)).Returns(expected);

        var svc = new DocumentService(manager.Object);
        var result = svc.NewDocument(SwDocType.Part, custom);

        Assert.Equal(expected, result);
        // Must NOT read default template when one is supplied
        swApp.Verify(s => s.GetDefaultTemplatePath(It.IsAny<SwDocType>()), Times.Never);
        swApp.Verify(s => s.NewDoc(custom), Times.Once);
    }

    [Fact]
    public void NewDocument_SwReturnsNull_ThrowsInvalidOperation()
    {
        var (manager, swApp) = ConnectedMocks();
        swApp.Setup(s => s.GetDefaultTemplatePath(SwDocType.Part)).Returns("t.prtdot");
        swApp.Setup(s => s.NewDoc(It.IsAny<string>())).Returns((SwDocumentInfo?)null);

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.NewDocument(SwDocType.Part));
    }

    [Fact]
    public void NewDocument_NotConnected_Throws()
    {
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.EnsureConnected())
               .Throws(new InvalidOperationException("Not connected"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.NewDocument(SwDocType.Part));
    }

    // ─────────────────────────────────────────────────────────────
    // OpenDocument
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void OpenDocument_ValidPath_ReturnsOpenResult()
    {
        var (manager, swApp) = ConnectedMocks();
        const string path = @"C:\part.sldprt";
        var expected = FakeOpenResult(path);
        swApp.Setup(s => s.OpenDoc(path)).Returns(expected);

        var svc = new DocumentService(manager.Object);
        var result = svc.OpenDocument(path);

        Assert.Equal(expected, result);
        swApp.Verify(s => s.OpenDoc(path), Times.Once);
    }

    [Fact]
    public void OpenDocument_SwReturnsNull_ThrowsInvalidOperation()
    {
        var (manager, swApp) = ConnectedMocks();
        swApp.Setup(s => s.OpenDoc(It.IsAny<string>()))
            .Throws(new InvalidOperationException("SolidWorks failed to open document"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.OpenDocument(@"C:\missing.sldprt"));
    }

    [Fact]
    public void OpenDocument_NotConnected_Throws()
    {
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.EnsureConnected())
               .Throws(new InvalidOperationException("Not connected"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.OpenDocument(@"C:\x.sldprt"));
    }

    // ─────────────────────────────────────────────────────────────
    // CloseDocument
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void CloseDocument_CallsCloseDoc()
    {
        var (manager, swApp) = ConnectedMocks();
        const string path = @"C:\part.sldprt";

        var svc = new DocumentService(manager.Object);
        svc.CloseDocument(path);

        swApp.Verify(s => s.CloseDoc(path), Times.Once);
    }

    [Fact]
    public void CloseDocument_NotConnected_Throws()
    {
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.EnsureConnected())
               .Throws(new InvalidOperationException("Not connected"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.CloseDocument(@"C:\x.sldprt"));
    }

    // ─────────────────────────────────────────────────────────────
    // SaveDocument
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void SaveDocument_CallsSaveDoc()
    {
        var (manager, swApp) = ConnectedMocks();
        const string path = @"C:\part.sldprt";
        var expected = new SwSaveResult(path, path, "sldprt", false, 0, 0,
            new SwApiDiagnostics(0, Array.Empty<SwCodeInfo>(), 0, Array.Empty<SwCodeInfo>()));
        swApp.Setup(s => s.SaveDoc(path)).Returns(expected);

        var svc = new DocumentService(manager.Object);
        var result = svc.SaveDocument(path);

        Assert.Equal(expected, result);
        swApp.Verify(s => s.SaveDoc(path), Times.Once);
    }

    [Fact]
    public void SaveDocumentAs_DelegatesToWrapper()
    {
        var (manager, swApp) = ConnectedMocks();
        const string sourcePath = @"C:\part.sldprt";
        const string outputPath = @"C:\exports\part.step";
        var expected = new SwSaveResult(sourcePath, outputPath, "step", true, 0, 0);
        swApp.Setup(s => s.SaveDocAs(outputPath, sourcePath, true)).Returns(expected);

        var svc = new DocumentService(manager.Object);
        var result = svc.SaveDocumentAs(outputPath, sourcePath, true);

        Assert.Equal(expected, result);
        swApp.Verify(s => s.SaveDocAs(outputPath, sourcePath, true), Times.Once);
    }

    [Fact]
    public void Undo_CallsWrapperUndo()
    {
        var (manager, swApp) = ConnectedMocks();

        var svc = new DocumentService(manager.Object);
        svc.Undo(3);

        swApp.Verify(s => s.Undo(3), Times.Once);
    }

    [Fact]
    public void ShowStandardView_CallsWrapper()
    {
        var (manager, swApp) = ConnectedMocks();

        var svc = new DocumentService(manager.Object);
        svc.ShowStandardView(SwStandardView.Top);

        swApp.Verify(s => s.ShowStandardView(SwStandardView.Top), Times.Once);
    }

    [Fact]
    public void RotateView_CallsWrapper()
    {
        var (manager, swApp) = ConnectedMocks();

        var svc = new DocumentService(manager.Object);
        svc.RotateView(10, -5, 30);

        swApp.Verify(s => s.RotateView(10, -5, 30), Times.Once);
    }

    [Fact]
    public void ExportCurrentViewPng_CallsWrapper()
    {
        var (manager, swApp) = ConnectedMocks();
        const string outputPath = @"C:\exports\view.png";
        var expected = new SwImageExportResult(outputPath, "image/png", 800, 600, null);
        swApp.Setup(s => s.ExportCurrentViewPng(outputPath, 800, 600, false)).Returns(expected);

        var svc = new DocumentService(manager.Object);
        var result = svc.ExportCurrentViewPng(outputPath, 800, 600, false);

        Assert.Equal(expected, result);
        swApp.Verify(s => s.ExportCurrentViewPng(outputPath, 800, 600, false), Times.Once);
    }

    [Fact]
    public void SaveDocument_NotConnected_Throws()
    {
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.EnsureConnected())
               .Throws(new InvalidOperationException("Not connected"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.SaveDocument(@"C:\x.sldprt"));
    }

    [Fact]
    public void SaveDocumentAs_NotConnected_Throws()
    {
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.EnsureConnected())
               .Throws(new InvalidOperationException("Not connected"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.SaveDocumentAs(@"C:\x.step"));
    }

    [Fact]
    public void Undo_NotConnected_Throws()
    {
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.EnsureConnected())
               .Throws(new InvalidOperationException("Not connected"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.Undo());
    }

    [Fact]
    public void ShowStandardView_NotConnected_Throws()
    {
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.EnsureConnected())
               .Throws(new InvalidOperationException("Not connected"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.ShowStandardView(SwStandardView.Isometric));
    }

    [Fact]
    public void RotateView_NotConnected_Throws()
    {
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.EnsureConnected())
               .Throws(new InvalidOperationException("Not connected"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.RotateView(5, 0, 0));
    }

    [Fact]
    public void ExportCurrentViewPng_NotConnected_Throws()
    {
        var manager = new Mock<ISwConnectionManager>();
        manager.Setup(m => m.EnsureConnected())
               .Throws(new InvalidOperationException("Not connected"));

        var svc = new DocumentService(manager.Object);
        Assert.Throws<InvalidOperationException>(() => svc.ExportCurrentViewPng(@"C:\view.png"));
    }

    // ─────────────────────────────────────────────────────────────
    // ListDocuments
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void ListDocuments_ReturnsAllOpenDocs()
    {
        var (manager, swApp) = ConnectedMocks();
        var docs = new[]
        {
            FakeDoc(@"C:\a.sldprt"),
            FakeDoc(@"C:\b.sldasm", type: 2),
        };
        swApp.Setup(s => s.ListDocs()).Returns(docs);

        var svc = new DocumentService(manager.Object);
        var result = svc.ListDocuments();

        Assert.Equal(2, result.Length);
        Assert.Equal(@"C:\a.sldprt", result[0].Path);
        Assert.Equal(@"C:\b.sldasm", result[1].Path);
    }

    [Fact]
    public void ListDocuments_NoOpenDocs_ReturnsEmpty()
    {
        var (manager, swApp) = ConnectedMocks();
        swApp.Setup(s => s.ListDocs()).Returns(Array.Empty<SwDocumentInfo>());

        var svc = new DocumentService(manager.Object);
        var result = svc.ListDocuments();

        Assert.Empty(result);
    }

    // ─────────────────────────────────────────────────────────────
    // GetActiveDocument
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetActiveDocument_ReturnsActiveDoc()
    {
        var (manager, swApp) = ConnectedMocks();
        var expected = FakeDoc(@"C:\active.sldprt");
        swApp.Setup(s => s.GetActiveDoc()).Returns(expected);

        var svc = new DocumentService(manager.Object);
        var result = svc.GetActiveDocument();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetActiveDocument_NoActive_ReturnsNull()
    {
        var (manager, swApp) = ConnectedMocks();
        swApp.Setup(s => s.GetActiveDoc()).Returns((SwDocumentInfo?)null);

        var svc = new DocumentService(manager.Object);
        var result = svc.GetActiveDocument();

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────
    // Integration Tests (require real SolidWorks)
    // Run: dotnet test --filter "Category=Integration"
    // ─────────────────────────────────────────────────────────────

    private static DocumentService RealService()
    {
        var connector = new SwComConnector();
        var manager = new SwConnectionManager(connector);
        manager.Connect();
        return new DocumentService(manager);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_NewDocument_Part_CreatesDocument()
    {
        // Expected: returns SwDocumentInfo with Type=1, non-empty Path/Title
        using var ctx = new SolidWorksIntegrationTestContext();
        var svc = ctx.Documents;
        var doc = svc.NewDocument(SwDocType.Part);

        Assert.Equal(1, doc.Type);
        Assert.False(string.IsNullOrEmpty(doc.Title),
            "New Part document should have a non-empty Title");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_NewDocument_Assembly_CreatesDocument()
    {
        // Expected: returns SwDocumentInfo with Type=2
        using var ctx = new SolidWorksIntegrationTestContext();
        var svc = ctx.Documents;
        var doc = svc.NewDocument(SwDocType.Assembly);

        Assert.Equal(2, doc.Type);
        Assert.False(string.IsNullOrEmpty(doc.Title));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_ListDocuments_AfterNewDoc_CountIncreases()
    {
        // Expected: creating a new Part increases document count by 1
        using var ctx = new SolidWorksIntegrationTestContext();
        var svc = ctx.Documents;
        var before = svc.ListDocuments().Length;

        svc.NewDocument(SwDocType.Part);
        var after = svc.ListDocuments().Length;

        Assert.True(after >= before + 1,
            $"Expected at least {before + 1} open docs, got {after}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_GetActiveDocument_AfterNewDoc_IsNotNull()
    {
        // Expected: after creating a new document it becomes the active doc
        using var ctx = new SolidWorksIntegrationTestContext();
        var svc = ctx.Documents;
        svc.NewDocument(SwDocType.Part);

        var active = svc.GetActiveDocument();

        Assert.NotNull(active);
        Assert.Equal(1, active!.Type);
    }
}
