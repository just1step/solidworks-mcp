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
    SwDocumentInfo OpenDocument(string path);

    /// <summary>Close an open document by file path.</summary>
    void CloseDocument(string path);

    /// <summary>Save an open document by file path.</summary>
    void SaveDocument(string path);

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

    public SwDocumentInfo OpenDocument(string path)
    {
        _connectionManager.EnsureConnected();
        var sw = _connectionManager.SwApp!;

        var doc = sw.OpenDoc(path)
            ?? throw new InvalidOperationException(
                $"SolidWorks failed to open document: {path}");

        return doc;
    }

    public void CloseDocument(string path)
    {
        _connectionManager.EnsureConnected();
        _connectionManager.SwApp!.CloseDoc(path);
    }

    public void SaveDocument(string path)
    {
        _connectionManager.EnsureConnected();
        _connectionManager.SwApp!.SaveDoc(path);
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
