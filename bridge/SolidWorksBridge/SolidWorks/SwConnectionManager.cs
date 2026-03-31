using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

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

    /// <summary>
    /// Save or export a document to a new path. When <paramref name="sourcePath"/> is null,
    /// the active document is used.
    /// </summary>
    SwSaveResult SaveDocAs(string outputPath, string? sourcePath, bool saveAsCopy);

    /// <summary>Undo the last <paramref name="steps"/> operations on the active document.</summary>
    void Undo(int steps);

    /// <summary>Switch the active document to a standard orientation.</summary>
    void ShowStandardView(SwStandardView view);

    /// <summary>Rotate the active document view around the global x, y, and z axes.</summary>
    void RotateView(double xDegrees, double yDegrees, double zDegrees);

    /// <summary>Export the current active viewport to PNG.</summary>
    SwImageExportResult ExportCurrentViewPng(string outputPath, int width, int height, bool includeBase64Data);

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

    public SwSaveResult SaveDocAs(string outputPath, string? sourcePath, bool saveAsCopy)
    {
        var normalizedOutputPath = NormalizePath(outputPath, nameof(outputPath));
        EnsureDirectory(normalizedOutputPath);

        var doc = ResolveDocument(sourcePath);
        var sourceDocPath = doc.GetPathName();
        int errors = 0;
        int warnings = 0;
        int options = (int)swSaveAsOptions_e.swSaveAsOptions_Silent;
        if (saveAsCopy)
        {
            options |= (int)swSaveAsOptions_e.swSaveAsOptions_Copy;
        }

        bool saved = doc.Extension.SaveAs(
            normalizedOutputPath,
            (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            options,
            null,
            ref errors,
            ref warnings);

        if (!saved || !File.Exists(normalizedOutputPath))
        {
            throw new InvalidOperationException(
                $"SolidWorks failed to save document as '{normalizedOutputPath}'. Errors={errors}, Warnings={warnings}.");
        }

        return new SwSaveResult(
            sourceDocPath,
            normalizedOutputPath,
            Path.GetExtension(normalizedOutputPath).TrimStart('.').ToLowerInvariant(),
            saveAsCopy,
            errors,
            warnings);
    }

    public void Undo(int steps)
    {
        if (steps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), "Undo steps must be at least 1.");
        }

        var doc = RequireActiveDocument();
        doc.EditUndo2(steps);
    }

    public void ShowStandardView(SwStandardView view)
    {
        var doc = RequireActiveDocument();
        var (viewName, viewId) = GetStandardView(view);
        doc.ShowNamedView2(viewName, viewId);
        doc.GraphicsRedraw2();
    }

    public void RotateView(double xDegrees, double yDegrees, double zDegrees)
    {
        if (xDegrees == 0 && yDegrees == 0 && zDegrees == 0)
        {
            throw new ArgumentException("At least one rotation angle must be non-zero.");
        }

        var doc = RequireActiveDocument();
        var view = doc.IActiveView
            ?? throw new InvalidOperationException("SolidWorks does not have an active model view.");

        if (xDegrees != 0 || yDegrees != 0)
        {
            view.RotateAboutCenter(ToRadians(xDegrees), ToRadians(yDegrees));
        }

        if (zDegrees != 0)
        {
            view.RotateAboutAxis(ToRadians(zDegrees), 0, 0, 0, 0, 0, 1);
        }

        doc.GraphicsRedraw2();
    }

    public SwImageExportResult ExportCurrentViewPng(string outputPath, int width, int height, bool includeBase64Data)
    {
        if (width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be at least 1.");
        }

        if (height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be at least 1.");
        }

        var normalizedOutputPath = NormalizePath(outputPath, nameof(outputPath));
        EnsureDirectory(normalizedOutputPath);

        var doc = RequireActiveDocument();
        var tempBmpPath = Path.Combine(Path.GetTempPath(), $"solidworks-mcp-{Guid.NewGuid():N}.bmp");

        try
        {
            if (!doc.SaveBMP(tempBmpPath, width, height) || !File.Exists(tempBmpPath))
            {
                throw new InvalidOperationException($"SolidWorks failed to export the current view to bitmap: {tempBmpPath}");
            }

            using (var bitmap = new Bitmap(tempBmpPath))
            {
                bitmap.Save(normalizedOutputPath, ImageFormat.Png);
            }

            var base64Data = includeBase64Data
                ? Convert.ToBase64String(File.ReadAllBytes(normalizedOutputPath))
                : null;

            return new SwImageExportResult(normalizedOutputPath, "image/png", width, height, base64Data);
        }
        finally
        {
            if (File.Exists(tempBmpPath))
            {
                File.Delete(tempBmpPath);
            }
        }
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

    private IModelDoc2 ResolveDocument(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return RequireActiveDocument();
        }

        var normalizedSourcePath = NormalizePath(sourcePath, nameof(sourcePath));
        return _swApp.GetOpenDocument(normalizedSourcePath) as IModelDoc2
            ?? throw new InvalidOperationException($"Document not open: {normalizedSourcePath}");
    }

    private IModelDoc2 RequireActiveDocument() =>
        _swApp.IActiveDoc2
            ?? throw new InvalidOperationException("No active document.");

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string NormalizePath(string path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", paramName);
        }

        return Path.GetFullPath(path);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static (string ViewName, int ViewId) GetStandardView(SwStandardView view) => view switch
    {
        SwStandardView.Front => ("*Front", (int)swStandardViews_e.swFrontView),
        SwStandardView.Back => ("*Back", (int)swStandardViews_e.swBackView),
        SwStandardView.Left => ("*Left", (int)swStandardViews_e.swLeftView),
        SwStandardView.Right => ("*Right", (int)swStandardViews_e.swRightView),
        SwStandardView.Top => ("*Top", (int)swStandardViews_e.swTopView),
        SwStandardView.Bottom => ("*Bottom", (int)swStandardViews_e.swBottomView),
        SwStandardView.Isometric => ("*Isometric", (int)swStandardViews_e.swIsometricView),
        SwStandardView.Trimetric => ("*Trimetric", (int)swStandardViews_e.swTrimetricView),
        SwStandardView.Dimetric => ("*Dimetric", (int)swStandardViews_e.swDimetricView),
        _ => throw new ArgumentOutOfRangeException(nameof(view))
    };

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
