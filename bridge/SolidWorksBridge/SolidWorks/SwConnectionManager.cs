using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksBridge.SolidWorks;

public record SwBuildNumbers(string BaseVersion, string CurrentVersion, string HotFixes);

public record SolidWorksRuntimeVersionInfo(
    string RevisionNumber,
    int? RevisionMajor,
    int? ServicePack,
    int? Hotfix,
    int? MarketingYear,
    SwBuildNumbers BuildNumbers,
    string ExecutablePath);

public record SolidWorksLicenseInfo(
    int Value,
    string Name,
    string Description);

public record SolidWorksCompatibilityInfo(
    string CompatibilityState,
    string Summary,
    string InteropVersion,
    int InteropRevisionMajor,
    int? InteropMarketingYear,
    SolidWorksRuntimeVersionInfo RuntimeVersion,
    SolidWorksLicenseInfo License,
    IReadOnlyList<string> Notices,
    SolidWorksVersionSupportInfo? RuntimeSupport = null,
    SolidWorksConnectionVersionCheck? ConnectionVersionCheck = null);

public record SolidWorksConnectionVersionCheck(
    string Status,
    string Message,
    bool IsSupportedBaseline);

public record SolidWorksConnectionAttemptInfo(
    string ConnectionSource,
    bool RunningProcessDetected,
    string? ProgId,
    string Summary);

/// <summary>
/// Abstraction over the SolidWorks application COM object.
/// Allows mocking in unit tests without requiring a real SolidWorks instance.
/// </summary>
public interface ISldWorksApp
{
    // ── Connection ────────────────────────────────────────────────
    bool Visible { get; set; }
    string GetCurrentLanguage();
    string GetRevisionNumber();
    SwBuildNumbers GetBuildNumbers();
    string GetExecutablePath();
    int GetCurrentLicenseType();
    int GetDocumentCount();
    string[] GetDocuments();
    void CloseAllDocuments(bool save);

    // ── Document operations (used by DocumentService) ─────────────
    /// <summary>Create a new document from a template file.</summary>
    SwDocumentInfo? NewDoc(string templatePath);

    /// <summary>Open an existing document by file path.</summary>
    SwOpenResult OpenDoc(string path);

    /// <summary>Activate an open document by file path.</summary>
    SwDocumentInfo ActivateDoc(string path);

    /// <summary>Close a document by file path.</summary>
    void CloseDoc(string path);

    /// <summary>Save an open document by file path (silent, no dialogs).</summary>
    SwSaveResult SaveDoc(string path);

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

    bool HasRunningProcess();

    string? LastResolvedProgId { get; }

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
    SolidWorksConnectionAttemptInfo? LastConnectionAttempt { get; }
    void Connect();
    void Disconnect();
    void EnsureConnected();
    SolidWorksCompatibilityInfo GetCompatibilityInfo();
}

/// <summary>
/// Default COM connector that uses Marshal.GetActiveObject / Activator.CreateInstance.
/// This is the real implementation used in production.
/// </summary>
public class SwComConnector : ISwComConnector
{
    public string? LastResolvedProgId { get; private set; }

    public ISldWorksApp? GetActiveInstance()
    {
        LastResolvedProgId = null;

        if (!HasRunningProcess())
        {
            return null;
        }

        foreach (string progId in GetCandidateProgIds(preferVersionSpecific: true))
        {
            try
            {
                var obj = GetActiveObject(progId);
                if (obj != null)
                {
                    LastResolvedProgId = progId;
                    return new SldWorksAppWrapper(obj);
                }
            }
            catch
            {
                // Try the next registered ProgID. This handles stale version-independent
                // mappings left behind after uninstalling another SolidWorks version.
            }
        }

        return null;
    }

    public bool HasRunningProcess()
    {
        try
        {
            return Process.GetProcessesByName("SLDWORKS").Any(static process =>
            {
                try
                {
                    return !process.HasExited;
                }
                catch
                {
                    return false;
                }
            });
        }
        catch
        {
            return false;
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
        LastResolvedProgId = null;

        if (HasRunningProcess())
        {
            throw new InvalidOperationException(
                "Detected a running SolidWorks process but could not attach to it via COM. Refusing to launch another SolidWorks instance.");
        }

        List<string> errors = [];

        foreach (string progId in GetCandidateProgIds(preferVersionSpecific: true))
        {
            var swType = Type.GetTypeFromProgID(progId);
            if (swType == null)
            {
                continue;
            }

            try
            {
                var obj = Activator.CreateInstance(swType);
                if (obj != null)
                {
                    LastResolvedProgId = progId;
                    return new SldWorksAppWrapper(obj);
                }

                errors.Add($"{progId}: Activator.CreateInstance returned null.");
            }
            catch (Exception ex)
            {
                errors.Add($"{progId}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (errors.Count == 0)
        {
            throw new InvalidOperationException("SolidWorks is not installed or not registered");
        }

        throw new InvalidOperationException(
            "Failed to create a SolidWorks instance from any registered ProgID. " + string.Join(" ; ", errors));
    }

    private static IReadOnlyList<string> GetCandidateProgIds(bool preferVersionSpecific)
    {
        const string genericProgId = "SldWorks.Application";
        const string prefix = "SldWorks.Application.";

        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var versionedProgIds = Registry.ClassesRoot
                .GetSubKeyNames()
                .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(name => new
                {
                    ProgId = name,
                    Match = Regex.Match(name, @"^SldWorks\.Application\.(\d+)$", RegexOptions.IgnoreCase)
                })
                .Where(entry => entry.Match.Success)
                .OrderByDescending(entry => int.Parse(entry.Match.Groups[1].Value, CultureInfo.InvariantCulture))
                .Select(entry => entry.ProgId);

            foreach (string progId in versionedProgIds)
            {
                if (seen.Add(progId))
                {
                    candidates.Add(progId);
                }
            }
        }
        catch
        {
            // Registry enumeration is best-effort.
        }

        if (!preferVersionSpecific && seen.Add(genericProgId))
        {
            candidates.Insert(0, genericProgId);
        }
        else if (seen.Add(genericProgId))
        {
            candidates.Add(genericProgId);
        }

        return candidates;
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

    public string GetRevisionNumber() => _swApp.RevisionNumber();

    public SwBuildNumbers GetBuildNumbers()
    {
        _swApp.GetBuildNumbers2(out string baseVersion, out string currentVersion, out string hotFixes);
        return new SwBuildNumbers(
            baseVersion ?? string.Empty,
            currentVersion ?? string.Empty,
            hotFixes ?? string.Empty);
    }

    public string GetExecutablePath() => _swApp.GetExecutablePath();

    public int GetCurrentLicenseType() => _swApp.GetCurrentLicenseType();

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

    public SwOpenResult OpenDoc(string path)
    {
        // Infer document type from file extension
        int docType = InferDocType(path);
        // swOpenDocOptions_Silent = 1
        int errors = 0, warnings = 0;
        var doc = _swApp.OpenDoc6(path, docType, 1, "", ref errors, ref warnings);
        var diagnostics = SolidWorksApiErrorFactory.CreateLoadDiagnostics(errors, warnings);

        if (doc == null)
        {
            throw SolidWorksApiExceptionFromLoad(path, docType, diagnostics);
        }

        return new SwOpenResult(ToInfo(doc), diagnostics);
    }

    public SwDocumentInfo ActivateDoc(string path)
    {
        var normalizedPath = NormalizePath(path, nameof(path));
        _ = _swApp.GetOpenDocument(normalizedPath) as IModelDoc2
            ?? throw new InvalidOperationException($"Document not open: {normalizedPath}");

        int errors = 0;
        var doc = _swApp.ActivateDoc3(
            Path.GetFileName(normalizedPath),
            true,
            (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
            ref errors);

        if (doc == null)
        {
            throw new InvalidOperationException($"Failed to activate document '{normalizedPath}'. SolidWorks error code: {errors}.");
        }

        return ToInfo((IModelDoc2)doc);
    }

    public void CloseDoc(string path) => _swApp.CloseDoc(path);

    public SwSaveResult SaveDoc(string path)
    {
        var doc = _swApp.GetOpenDocument(path) as IModelDoc2
            ?? throw new InvalidOperationException($"Document not open: {path}");
        int errors = 0, warnings = 0;
        // swSaveAsOptions_Silent = 1
        bool saved = doc.Save3(1, ref errors, ref warnings);
        var diagnostics = SolidWorksApiErrorFactory.CreateSaveDiagnostics(errors, warnings);
        if (!saved)
        {
            throw SolidWorksApiErrorFactory.FromSaveFailure(
                "IModelDoc2.Save3",
                $"Failed to save document '{path}'.",
                errors,
                warnings,
                new Dictionary<string, object?> { ["path"] = path });
        }

        return new SwSaveResult(
            doc.GetPathName(),
            doc.GetPathName(),
            Path.GetExtension(doc.GetPathName()).TrimStart('.').ToLowerInvariant(),
            false,
            errors,
            warnings,
            diagnostics);
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

        bool saved = doc.Extension.SaveAs3(
            normalizedOutputPath,
            (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            options,
            null,
            null,
            ref errors,
            ref warnings);

        var diagnostics = SolidWorksApiErrorFactory.CreateSaveDiagnostics(errors, warnings);

        if (!saved || !File.Exists(normalizedOutputPath))
        {
            throw SolidWorksApiErrorFactory.FromSaveFailure(
                "IModelDocExtension.SaveAs3",
                $"Failed to save document as '{normalizedOutputPath}'.",
                errors,
                warnings,
                new Dictionary<string, object?>
                {
                    ["outputPath"] = normalizedOutputPath,
                    ["sourcePath"] = sourceDocPath,
                    ["saveAsCopy"] = saveAsCopy,
                });
        }

        return new SwSaveResult(
            sourceDocPath,
            normalizedOutputPath,
            Path.GetExtension(normalizedOutputPath).TrimStart('.').ToLowerInvariant(),
            saveAsCopy,
            errors,
            warnings,
            diagnostics);
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
        doc.ViewZoomtofit2();
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

        doc.ViewZoomtofit2();
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
            doc.ViewZoomtofit2();
            doc.GraphicsRedraw2();

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
        return SolidWorksTemplateLocator.ResolveDefaultTemplatePath(
            _swApp.GetUserPreferenceStringValue(prefId),
            docType,
            _swApp.GetExecutablePath());
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static SwDocumentInfo ToInfo(IModelDoc2 doc) =>
        new(doc.GetPathName(), doc.GetTitle(), doc.GetType());

    private static SolidWorksApiException SolidWorksApiExceptionFromLoad(string path, int docType, SwApiDiagnostics diagnostics)
    {
        return SolidWorksApiErrorFactory.FromLoadFailure(
            "ISldWorks.OpenDoc6",
            $"Failed to open document '{path}'.",
            diagnostics.RawErrorCode,
            diagnostics.RawWarningCode,
            new Dictionary<string, object?>
            {
                ["path"] = path,
                ["docType"] = docType,
            });
    }

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
    private const int RpcServerUnavailable = unchecked((int)0x800706BA);
    private const int RpcCallFailed = unchecked((int)0x800706BE);
    private const int RpcDisconnected = unchecked((int)0x80010108);
    private static readonly Version InteropAssemblyVersion = typeof(ISldWorks).Assembly.GetName().Version ?? new Version(0, 0);
    private static readonly string InteropVersion = FormatInteropVersion(InteropAssemblyVersion);
    private static readonly int InteropRevisionMajor = InteropAssemblyVersion.Major;
    private static readonly int? InteropMarketingYear = TryGetMarketingYear(InteropRevisionMajor);

    public static string CompiledInteropVersion => InteropVersion;
    public static int CompiledInteropRevisionMajor => InteropRevisionMajor;
    public static int? CompiledInteropMarketingYear => InteropMarketingYear;
    public static SolidWorksSupportMatrixInfo GetCompiledSupportMatrix() =>
        SolidWorksSupportMatrix.Create(InteropVersion, InteropRevisionMajor, InteropMarketingYear);

    public SwConnectionManager(ISwComConnector connector)
    {
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
    }

    public bool IsConnected => _swApp != null;

    public ISldWorksApp? SwApp => _swApp;

    public SolidWorksConnectionAttemptInfo? LastConnectionAttempt { get; private set; }

    public void Connect()
    {
        if (TryUseCurrentSession())
        {
            LastConnectionAttempt = new SolidWorksConnectionAttemptInfo(
                "current-session",
                _connector.HasRunningProcess(),
                _connector.LastResolvedProgId,
                "Reused the existing in-process SolidWorks COM session.");
            return;
        }

        bool runningProcessDetected = _connector.HasRunningProcess();

        // Try to attach to a live SolidWorks process first.
        var runningInstance = _connector.GetActiveInstance();

        if (runningInstance != null && IsConnectionAlive(runningInstance))
        {
            _swApp = runningInstance;
            _swApp.Visible = true;
            LastConnectionAttempt = new SolidWorksConnectionAttemptInfo(
                "running-process",
                runningProcessDetected,
                _connector.LastResolvedProgId,
                "Attached to a running SolidWorks process via COM.");
            return;
        }

        try
        {
            // If no process is alive, launch the newest registered version first.
            _swApp = _connector.CreateNewInstance();
            _swApp.Visible = true;
            LastConnectionAttempt = new SolidWorksConnectionAttemptInfo(
                "new-instance",
                runningProcessDetected,
                _connector.LastResolvedProgId,
                "Launched a new SolidWorks instance from the newest registered COM ProgID.");
        }
        catch
        {
            LastConnectionAttempt = new SolidWorksConnectionAttemptInfo(
                "connect-failed",
                runningProcessDetected,
                _connector.LastResolvedProgId,
                runningProcessDetected
                    ? "A SolidWorks process was running, but the bridge could not attach to it via COM."
                    : "The bridge could not launch any registered SolidWorks COM server.");
            throw;
        }
    }

    public void Disconnect()
    {
        _swApp = null;
        LastConnectionAttempt = null;
    }

    public void EnsureConnected()
    {
        Connect();

        if (!IsConnected)
            throw SolidWorksApiErrorFactory.NotConnected();
    }

    public SolidWorksCompatibilityInfo GetCompatibilityInfo()
    {
        EnsureConnected();

        var runtimeVersion = CreateRuntimeVersionInfo(_swApp!);
        var license = CreateLicenseInfo(_swApp!);
        var notices = new List<string>
        {
            $"Bridge interop baseline is revision {InteropRevisionMajor} ({FormatMarketingYear(InteropMarketingYear)}) via SolidWorks.Interop.* {InteropVersion}."
        };

        var (compatibilityState, summary) = ClassifyCompatibility(runtimeVersion, notices);
        var runtimeSupport = SolidWorksSupportMatrix.ResolveRuntimeSupport(
            InteropVersion,
            InteropRevisionMajor,
            InteropMarketingYear,
            runtimeVersion,
            compatibilityState);
        var connectionVersionCheck = CreateConnectionVersionCheck(runtimeVersion, runtimeSupport);
        notices.Add(connectionVersionCheck.Message);

        return new SolidWorksCompatibilityInfo(
            compatibilityState,
            summary,
            InteropVersion,
            InteropRevisionMajor,
            InteropMarketingYear,
            runtimeVersion,
            license,
            notices,
                runtimeSupport,
                connectionVersionCheck);
    }

    private bool TryUseCurrentSession()
    {
        if (_swApp == null)
        {
            return false;
        }

        if (IsConnectionAlive(_swApp))
        {
            return true;
        }

        _swApp = null;
        return false;
    }

    private static bool IsConnectionAlive(ISldWorksApp swApp)
    {
        try
        {
            _ = swApp.GetDocumentCount();
            return true;
        }
        catch (COMException ex) when (IsDisconnectedComException(ex))
        {
            return false;
        }
        catch (COMException)
        {
            // Busy or modal SolidWorks sessions should remain connected.
            return true;
        }
    }

    private static bool IsDisconnectedComException(COMException ex)
        => ex.HResult == RpcServerUnavailable
        || ex.HResult == RpcCallFailed
        || ex.HResult == RpcDisconnected;

    private static SolidWorksConnectionVersionCheck CreateConnectionVersionCheck(
        SolidWorksRuntimeVersionInfo runtimeVersion,
        SolidWorksVersionSupportInfo runtimeSupport)
    {
        int? runtimeYear = runtimeVersion.MarketingYear;
        int? baselineYear = InteropMarketingYear;
        int? targetedYear = baselineYear.HasValue ? baselineYear.Value + 1 : null;
        int? experimentalYear = baselineYear.HasValue ? baselineYear.Value + 2 : null;

        if (string.Equals(runtimeSupport.ProductSupportLevel, "certified", StringComparison.OrdinalIgnoreCase)
            && runtimeYear.HasValue)
        {
            return new SolidWorksConnectionVersionCheck(
                $"supported-{runtimeYear.Value}-baseline",
                $"SolidWorks {runtimeYear.Value} is the certified interop baseline for MCP connection in this bridge build.",
                true);
        }

        if (string.Equals(runtimeSupport.ProductSupportLevel, "targeted", StringComparison.OrdinalIgnoreCase)
            && runtimeYear.HasValue)
        {
            return new SolidWorksConnectionVersionCheck(
                $"targeted-{runtimeYear.Value}",
                $"SolidWorks {runtimeYear.Value} is inside the targeted certification window for this bridge build. Connection and active workflow validation are supported, but this version is not the certified baseline yet.",
                false);
        }

        if (string.Equals(runtimeSupport.ProductSupportLevel, "experimental", StringComparison.OrdinalIgnoreCase)
            && runtimeYear.HasValue)
        {
            return new SolidWorksConnectionVersionCheck(
                $"experimental-{runtimeYear.Value}",
                $"SolidWorks {runtimeYear.Value} is inside the experimental discovery window for this bridge build. Use connection and read-only workflows for evidence gathering; high-risk mutation workflows remain blocked.",
                false);
        }

        if (string.Equals(runtimeSupport.ProductSupportLevel, "unsupported", StringComparison.OrdinalIgnoreCase)
            && runtimeYear.HasValue
            && baselineYear.HasValue
            && runtimeYear.Value < baselineYear.Value)
        {
            return new SolidWorksConnectionVersionCheck(
                $"unsupported-before-{baselineYear.Value}",
                $"SolidWorks versions earlier than {baselineYear.Value} are not supported for MCP connection in this bridge build.",
                false);
        }

        if (string.Equals(runtimeSupport.ProductSupportLevel, "unsupported", StringComparison.OrdinalIgnoreCase)
            && runtimeYear.HasValue
            && experimentalYear.HasValue
            && runtimeYear.Value > experimentalYear.Value)
        {
            return new SolidWorksConnectionVersionCheck(
                $"unsupported-after-{experimentalYear.Value}",
                $"SolidWorks {runtimeYear.Value} is newer than the current experimental discovery window for this bridge build. Update the support matrix before treating it as a supported connection target.",
                false);
        }

        return new SolidWorksConnectionVersionCheck(
            "unknown-version",
            CreateUnknownConnectionVersionMessage(baselineYear, targetedYear, experimentalYear),
            false);
    }

    private static string CreateUnknownConnectionVersionMessage(int? baselineYear, int? targetedYear, int? experimentalYear)
    {
        if (baselineYear.HasValue && targetedYear.HasValue && experimentalYear.HasValue)
        {
            return $"The running SolidWorks version could not be classified precisely. SolidWorks {baselineYear.Value} is the certified baseline, {targetedYear.Value} is the targeted next version, and {experimentalYear.Value} is the experimental discovery window for this bridge build.";
        }

        return "The running SolidWorks version could not be classified precisely, so this bridge cannot determine whether the current connection is baseline, targeted, experimental, or unsupported.";
    }

    private static SolidWorksRuntimeVersionInfo CreateRuntimeVersionInfo(ISldWorksApp swApp)
    {
        string revisionNumber = swApp.GetRevisionNumber();
        var buildNumbers = swApp.GetBuildNumbers();
        string executablePath = swApp.GetExecutablePath();

        ParseRevisionNumber(revisionNumber, out int? revisionMajor, out int? servicePack, out int? hotfix);

        return new SolidWorksRuntimeVersionInfo(
            revisionNumber,
            revisionMajor,
            servicePack,
            hotfix,
            TryGetMarketingYear(revisionMajor),
            buildNumbers,
            executablePath);
    }

    private static SolidWorksLicenseInfo CreateLicenseInfo(ISldWorksApp swApp)
    {
        int rawLicenseType = swApp.GetCurrentLicenseType();
        if (Enum.IsDefined(typeof(swLicenseType_e), rawLicenseType))
        {
            var licenseType = (swLicenseType_e)rawLicenseType;
            return new SolidWorksLicenseInfo(
                rawLicenseType,
                licenseType.ToString(),
                DescribeLicenseType(licenseType));
        }

        return new SolidWorksLicenseInfo(
            rawLicenseType,
            "UnknownLicenseType",
            "SolidWorks returned a license type that is not defined in swLicenseType_e.");
    }

    private static (string CompatibilityState, string Summary) ClassifyCompatibility(
        SolidWorksRuntimeVersionInfo runtimeVersion,
        List<string> notices)
    {
        if (runtimeVersion.RevisionMajor is null)
        {
            notices.Add("The running SolidWorks revision number could not be parsed, so compatibility remains unknown.");
            return (
                "unknown-version",
                "The running SolidWorks revision number could not be parsed, so this bridge cannot classify compatibility yet.");
        }

        if (runtimeVersion.RevisionMajor == InteropRevisionMajor)
        {
            notices.Add($"Runtime revision matches the interop baseline ({InteropRevisionMajor}).");
            return (
                "certified-baseline",
                $"The running SolidWorks session matches the bridge's validated interop baseline ({FormatMarketingYear(runtimeVersion.MarketingYear)})." );
        }

        if (runtimeVersion.RevisionMajor == InteropRevisionMajor + 1)
        {
            notices.Add($"Runtime revision is one major release newer than the interop baseline ({InteropRevisionMajor} -> {runtimeVersion.RevisionMajor}).");
            notices.Add("This next major version is in the planned certification window, but it is not yet declared fully validated in this repository.");
            return (
                "planned-next-version",
                $"The running SolidWorks session is one major release newer than the validated baseline ({FormatMarketingYear(runtimeVersion.MarketingYear)}), so it still needs certification.");
        }

        if (runtimeVersion.RevisionMajor > InteropRevisionMajor + 1)
        {
            notices.Add("Runtime revision is newer than the current validated and next-version certification window.");
            return (
                "unsupported-newer-version",
                $"The running SolidWorks session is newer than the validated compatibility window for this bridge build ({FormatMarketingYear(runtimeVersion.MarketingYear)})." );
        }

        notices.Add("Runtime revision is older than the compiled interop baseline and is not declared validated in this repository.");
        return (
            "unsupported-older-version",
            $"The running SolidWorks session is older than the bridge's validated interop baseline ({FormatMarketingYear(runtimeVersion.MarketingYear)}).");
    }

    private static void ParseRevisionNumber(string revisionNumber, out int? revisionMajor, out int? servicePack, out int? hotfix)
    {
        revisionMajor = null;
        servicePack = null;
        hotfix = null;

        if (string.IsNullOrWhiteSpace(revisionNumber))
        {
            return;
        }

        var segments = revisionNumber.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && int.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedRevisionMajor))
        {
            revisionMajor = parsedRevisionMajor;
        }

        if (segments.Length > 1 && int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedServicePack))
        {
            servicePack = parsedServicePack;
        }

        if (segments.Length > 2 && int.TryParse(segments[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedHotfix))
        {
            hotfix = parsedHotfix;
        }
    }

    private static int? TryGetMarketingYear(int? revisionMajor)
        => revisionMajor is >= 8 ? revisionMajor + 1992 : null;

    private static string FormatMarketingYear(int? marketingYear)
        => marketingYear?.ToString(CultureInfo.InvariantCulture) ?? "unknown marketing year";

    private static string FormatInteropVersion(Version version)
        => version.Build >= 0
            ? version.ToString(3)
            : version.ToString(2);

    private static string DescribeLicenseType(swLicenseType_e licenseType) => licenseType switch
    {
        swLicenseType_e.swLicenseType_Full => "Full SolidWorks license.",
        swLicenseType_e.swLicenseType_Educational => "Educational SolidWorks license.",
        swLicenseType_e.swLicenseType_Student => "Student SolidWorks license.",
        swLicenseType_e.swLicenseType_StudentDesignKit => "Student Design Kit license.",
        swLicenseType_e.swLicenseType_PersonalEdition => "Personal Edition SolidWorks license.",
        swLicenseType_e.swLicenseType_Full_Office => "SolidWorks Office license.",
        swLicenseType_e.swLicenseType_Full_Professional => "SolidWorks Professional license.",
        swLicenseType_e.swLicenseType_Full_Premium => "SolidWorks Premium license.",
        swLicenseType_e.swLicenseType_Maker => "SolidWorks Maker license.",
        _ => "Recognized SolidWorks license type."
    };
}
