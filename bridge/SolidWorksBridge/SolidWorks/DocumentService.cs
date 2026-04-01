using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.SolidWorks;

/// <summary>
/// Type of SolidWorks document. Values match swDocumentTypes_e from the SW API.
/// </summary>
public enum SwDocType
{
    Part = 1,
    Assembly = 2,
    Drawing = 3
}

/// <summary>
/// Lightweight descriptor for an open SolidWorks document.
/// </summary>
public record SwDocumentInfo(string Path, string Title, int Type);

public record SwOpenResult(SwDocumentInfo Document, SwApiDiagnostics Diagnostics);

/// <summary>
/// Standard model orientations supported by SolidWorks.
/// </summary>
public enum SwStandardView
{
    Front,
    Back,
    Left,
    Right,
    Top,
    Bottom,
    Isometric,
    Trimetric,
    Dimetric
}

/// <summary>
/// Save/export result metadata.
/// </summary>
public record SwSaveResult(string SourcePath, string OutputPath, string Format, bool SaveAsCopy, int Errors, int Warnings, SwApiDiagnostics? Diagnostics = null);

/// <summary>
/// Exported view image metadata.
/// </summary>
public record SwImageExportResult(string OutputPath, string MimeType, int Width, int Height, string? Base64Data);

/// <summary>
/// High-level document operations exposed to the MCP layer.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Create a new document. Uses the SW default template for the given type
    /// when <paramref name="templatePath"/> is null.
    /// </summary>
    SwDocumentInfo NewDocument(SwDocType docType, string? templatePath = null);

    /// <summary>Open an existing document by file path.</summary>
    SwOpenResult OpenDocument(string path);

    /// <summary>Close an open document by file path.</summary>
    void CloseDocument(string path);

    /// <summary>Save an open document by file path.</summary>
    SwSaveResult SaveDocument(string path);

    /// <summary>
    /// Save or export a document to a new path. The output format is inferred from
    /// the file extension, so this supports native SolidWorks files and formats like STEP or STL.
    /// When <paramref name="sourcePath"/> is null, the active document is used.
    /// </summary>
    SwSaveResult SaveDocumentAs(string outputPath, string? sourcePath = null, bool saveAsCopy = true);

    /// <summary>Undo the last <paramref name="steps"/> operations on the active document.</summary>
    void Undo(int steps = 1);

    /// <summary>Switch the active document to a standard orientation.</summary>
    void ShowStandardView(SwStandardView view);

    /// <summary>Rotate the active document view around the global x, y, and z axes.</summary>
    void RotateView(double xDegrees = 0, double yDegrees = 0, double zDegrees = 0);

    /// <summary>Export the current active viewport to PNG.</summary>
    SwImageExportResult ExportCurrentViewPng(string outputPath, int width = 1600, int height = 900, bool includeBase64Data = false);

    /// <summary>Return info for all currently open documents.</summary>
    SwDocumentInfo[] ListDocuments();

    /// <summary>Return info for the active document, or null if none is active.</summary>
    SwDocumentInfo? GetActiveDocument();
}

/// <summary>
/// Implements <see cref="IDocumentService"/> via <see cref="ISwConnectionManager"/>.
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly ISwConnectionManager _connectionManager;

    public DocumentService(ISwConnectionManager connectionManager)
    {
        _connectionManager = connectionManager
            ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public SwDocumentInfo NewDocument(SwDocType docType, string? templatePath = null)
    {
        _connectionManager.EnsureConnected();
        var sw = _connectionManager.SwApp!;

        var template = templatePath ?? sw.GetDefaultTemplatePath(docType);

        var doc = sw.NewDoc(template)
            ?? throw new InvalidOperationException(
                $"SolidWorks failed to create a new {docType} document using template: {template}");

        return doc;
    }

    public SwOpenResult OpenDocument(string path)
    {
        _connectionManager.EnsureConnected();
        return _connectionManager.SwApp!.OpenDoc(path);
    }

    public void CloseDocument(string path)
    {
        _connectionManager.EnsureConnected();
        _connectionManager.SwApp!.CloseDoc(path);
    }

    public SwSaveResult SaveDocument(string path)
    {
        _connectionManager.EnsureConnected();
        return _connectionManager.SwApp!.SaveDoc(path);
    }

    public SwSaveResult SaveDocumentAs(string outputPath, string? sourcePath = null, bool saveAsCopy = true)
    {
        _connectionManager.EnsureConnected();
        return _connectionManager.SwApp!.SaveDocAs(outputPath, sourcePath, saveAsCopy);
    }

    public void Undo(int steps = 1)
    {
        _connectionManager.EnsureConnected();
        _connectionManager.SwApp!.Undo(steps);
    }

    public void ShowStandardView(SwStandardView view)
    {
        _connectionManager.EnsureConnected();
        _connectionManager.SwApp!.ShowStandardView(view);
    }

    public void RotateView(double xDegrees = 0, double yDegrees = 0, double zDegrees = 0)
    {
        _connectionManager.EnsureConnected();
        _connectionManager.SwApp!.RotateView(xDegrees, yDegrees, zDegrees);
    }

    public SwImageExportResult ExportCurrentViewPng(string outputPath, int width = 1600, int height = 900, bool includeBase64Data = false)
    {
        _connectionManager.EnsureConnected();
        return _connectionManager.SwApp!.ExportCurrentViewPng(outputPath, width, height, includeBase64Data);
    }

    public SwDocumentInfo[] ListDocuments()
    {
        _connectionManager.EnsureConnected();
        return _connectionManager.SwApp!.ListDocs();
    }

    public SwDocumentInfo? GetActiveDocument()
    {
        _connectionManager.EnsureConnected();
        return _connectionManager.SwApp!.GetActiveDoc();
    }
}
