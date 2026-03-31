using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace SolidWorksBridge.SolidWorks;

/// <summary>
/// Abstraction over the SolidWorks application COM object.
/// Allows mocking in unit tests without requiring a real SolidWorks instance.
/// </summary>
public interface ISldWorksApp
{
    // ── Connection ────────────────────────────────────────────────
    bool Visible { get; set; }
    string GetCurrentLanguage();
    int GetDocumentCount();
    string[] GetDocuments();
    void CloseAllDocuments(bool save);

    // ── Document operations (used by DocumentService) ─────────────
    /// <summary>Create a new document from a template file.</summary>
    SwDocumentInfo? NewDoc(string templatePath);

    /// <summary>Open an existing document by file path.</summary>
    SwDocumentInfo? OpenDoc(string path);

    /// <summary>Close a document by file path.</summary>
    void CloseDoc(string path);

    /// <summary>Save an open document by file path (silent, no dialogs).</summary>
    void SaveDoc(string path);

    /// <summary>Return info for all open documents.</summary>
    SwDocumentInfo[] ListDocs();

    /// <summary>Return info for the currently active document, or null.</summary>
    SwDocumentInfo? GetActiveDoc();

    /// <summary>Return the user-configured default template path for the given doc type.</summary>
    string GetDefaultTemplatePath(SwDocType docType);

    /// <summary>
    /// Return the raw IModelDoc2 COM object for the active document.
    /// Used by services that need direct COM access (Selection, Sketch, Feature).
    /// </summary>
    IModelDoc2? IActiveDoc2 { get; }

    /// <summary>
    /// Return the ISketchManager of the active document, or null if no document is open.
    /// Used by SketchService for a cleanly mockable access path.
    /// </summary>
    ISketchManager? SketchManager { get; }

    /// <summary>
    /// Return the IFeatureManager of the active document, or null if no document is open.
    /// Used by FeatureService for a cleanly mockable access path.
    /// </summary>
    IFeatureManager? FeatureManager { get; }
}

/// <summary>
/// Abstraction for creating/obtaining the SolidWorks COM connection.
/// Separated from ISwConnectionManager so the connection strategy can be mocked.
/// </summary>
public interface ISwComConnector
{
    /// <summary>
    /// Try to get a running SolidWorks instance via COM ROT.
    /// Returns null if SolidWorks is not running.
    /// </summary>
    ISldWorksApp? GetActiveInstance();

    /// <summary>
    /// Create a new SolidWorks instance via COM activation.
    /// </summary>
    ISldWorksApp CreateNewInstance();
}

/// <summary>
/// Manages the connection to SolidWorks.
/// </summary>
public interface ISwConnectionManager
{
    bool IsConnected { get; }
    ISldWorksApp? SwApp { get; }
    void Connect();
    void Disconnect();
    void EnsureConnected();
}

/// <summary>
/// Default COM connector that uses Marshal.GetActiveObject / Activator.CreateInstance.
/// This is the real implementation used in production.
/// </summary>
public class SwComConnector : ISwComConnector
{
    public ISldWorksApp? GetActiveInstance()
    {
        try
        {
            var obj = GetActiveObject("SldWorks.Application");
            return obj != null ? new SldWorksAppWrapper(obj) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// .NET 8 compatible replacement for Marshal.GetActiveObject.
    /// Uses COM Running Object Table (ROT) directly.
    /// </summary>
    private static object? GetActiveObject(string progId)
    {
        var type = Type.GetTypeFromProgID(progId);
        if (type == null) return null;

        Guid clsid = type.GUID;
        int hr = GetActiveObject(ref clsid, IntPtr.Zero, out object? obj);
        return hr == 0 ? obj : null;
    }

    [DllImport("oleaut32.dll", PreserveSig = true)]
    private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object? ppunk);

    public ISldWorksApp CreateNewInstance()
    {
        var swType = Type.GetTypeFromProgID("SldWorks.Application")
            ?? throw new InvalidOperationException("SolidWorks is not installed or not registered");

        var obj = Activator.CreateInstance(swType)
            ?? throw new InvalidOperationException("Failed to create SolidWorks instance");

        return new SldWorksAppWrapper(obj);
    }
}

/// <summary>
/// Thin wrapper around the real SolidWorks COM object that implements ISldWorksApp.
/// Uses the strongly-typed SldWorks.ISldWorks interop interface to avoid
/// .NET 8 dynamic COM dispatch issues (TYPE_E_ELEMENTNOTFOUND).
/// </summary>
public class SldWorksAppWrapper : ISldWorksApp
{
    private readonly ISldWorks _swApp;

    public SldWorksAppWrapper(object swApp)
    {
        _swApp = (ISldWorks)(swApp
            ?? throw new ArgumentNullException(nameof(swApp)));
    }

    public bool Visible
    {
        get => _swApp.Visible;
        set => _swApp.Visible = value;
    }

    public string GetCurrentLanguage() => _swApp.GetCurrentLanguage();

    public int GetDocumentCount() => _swApp.GetDocumentCount();

    public string[] GetDocuments()
    {
        var result = _swApp.GetDocuments();
        if (result == null) return Array.Empty<string>();
        return ((object[])result)
            .OfType<IModelDoc2>()
            .Select(d => d.GetPathName())
            .ToArray();
    }

    public void CloseAllDocuments(bool save) => _swApp.CloseAllDocuments(!save);

    public SwDocumentInfo? NewDoc(string templatePath)
    {
        var doc = _swApp.INewDocument2(templatePath, 0, 0, 0);
        return doc == null ? null : ToInfo(doc);
    }

    public SwDocumentInfo? OpenDoc(string path)
    {
        // Infer document type from file extension
        int docType = InferDocType(path);
        // swOpenDocOptions_Silent = 1
        int errors = 0, warnings = 0;
        var doc = _swApp.OpenDoc6(path, docType, 1, "", ref errors, ref warnings);
        return doc == null ? null : ToInfo(doc);
    }

    public void CloseDoc(string path) => _swApp.CloseDoc(path);

    public void SaveDoc(string path)
    {
        var doc = _swApp.GetOpenDocument(path) as IModelDoc2
            ?? throw new InvalidOperationException($"Document not open: {path}");
        int errors = 0, warnings = 0;
        // swSaveAsOptions_Silent = 1
        doc.Save3(1, ref errors, ref warnings);
    }

    public SwDocumentInfo[] ListDocs()
    {
        var result = _swApp.GetDocuments();
        if (result == null) return Array.Empty<SwDocumentInfo>();
        return ((object[])result)
            .OfType<IModelDoc2>()
            .Select(ToInfo)
            .ToArray();
    }

    public SwDocumentInfo? GetActiveDoc()
    {
        var doc = _swApp.IActiveDoc2;
        return doc == null ? null : ToInfo(doc);
    }

    public IModelDoc2? IActiveDoc2 => _swApp.IActiveDoc2;

    public ISketchManager? SketchManager =>
        _swApp.IActiveDoc2?.SketchManager as ISketchManager;

    public IFeatureManager? FeatureManager =>
        _swApp.IActiveDoc2?.FeatureManager as IFeatureManager;

    public string GetDefaultTemplatePath(SwDocType docType)
    {
        // swDefaultTemplatePart=8, swDefaultTemplateAssembly=9, swDefaultTemplateDrawing=10
        int prefId = docType switch
        {
            SwDocType.Part => 8,
            SwDocType.Assembly => 9,
            SwDocType.Drawing => 10,
            _ => throw new ArgumentOutOfRangeException(nameof(docType))
        };
        return _swApp.GetUserPreferenceStringValue(prefId);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static SwDocumentInfo ToInfo(IModelDoc2 doc) =>
        new(doc.GetPathName(), doc.GetTitle(), doc.GetType());

    private static int InferDocType(string path) =>
        System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".sldasm" => 2,
            ".slddrw" => 3,
            _ => 1   // .sldprt or unknown → Part
        };
}

/// <summary>
/// Manages the lifecycle of the SolidWorks COM connection.
/// Uses ISwComConnector for the actual COM operations (mockable).
/// </summary>
public class SwConnectionManager : ISwConnectionManager
{
    private readonly ISwComConnector _connector;
    private ISldWorksApp? _swApp;

    public SwConnectionManager(ISwComConnector connector)
    {
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
    }

    public bool IsConnected => _swApp != null;

    public ISldWorksApp? SwApp => _swApp;

    public void Connect()
    {
        if (_swApp != null) return;

        // Try to attach to running instance first
        _swApp = _connector.GetActiveInstance();

        if (_swApp != null)
        {
            _swApp.Visible = true;
            return;
        }

        // Fallback: create new instance
        _swApp = _connector.CreateNewInstance();
        _swApp.Visible = true;
    }

    public void Disconnect()
    {
        _swApp = null;
    }

    public void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SolidWorks. Call Connect() first.");
    }
}
