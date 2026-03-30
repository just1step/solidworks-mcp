using SolidWorks.Interop.sldworks;

namespace SolidWorksBridge.SolidWorks;

/// <summary>
/// Result of a selection operation.
/// </summary>
public record SelectionResult(bool Success, string Message);

/// <summary>
/// Interface for selecting entities in the active document.
/// All sketch / feature / assembly operations depend on prior selection.
/// </summary>
public interface ISelectionService
{
    /// <summary>
    /// Select an entity by its name and type string (e.g. "Front Plane", "swSelDATUMPLANES").
    /// Coordinates default to 0,0,0 which is sufficient for named entities.
    /// </summary>
    SelectionResult SelectByName(string name, string selType);

    /// <summary>Clear the current selection set.</summary>
    void ClearSelection();
}

/// <summary>
/// Implements <see cref="ISelectionService"/> via <see cref="ISwConnectionManager"/>.
/// </summary>
public class SelectionService : ISelectionService
{
    private readonly ISwConnectionManager _cm;

    public SelectionService(ISwConnectionManager cm)
    {
        _cm = cm ?? throw new ArgumentNullException(nameof(cm));
    }

    public SelectionResult SelectByName(string name, string selType)
    {
        _cm.EnsureConnected();
        var doc = GetActiveModelDoc();
        // SelectByID(name, type, x, y, z) — x/y/z are 0,0,0 for named geometry
        bool ok = doc.SelectByID(name, selType, 0, 0, 0);
        return ok
            ? new SelectionResult(true, $"Selected '{name}'")
            : new SelectionResult(false, $"Could not select '{name}' (type: {selType})");
    }

    public void ClearSelection()
    {
        _cm.EnsureConnected();
        GetActiveModelDoc().ClearSelection2(true);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private IModelDoc2 GetActiveModelDoc()
    {
        return _cm.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException("No active document. Open or create a document first.");
    }
}
