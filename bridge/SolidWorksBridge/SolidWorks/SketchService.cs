using SolidWorks.Interop.sldworks;

namespace SolidWorksBridge.SolidWorks;

/// <summary>Info about a sketch entity that was just created.</summary>
public record SketchEntityInfo(string Type, double X1, double Y1, double X2, double Y2);

/// <summary>
/// Sketch operations on the active document.
/// Caller must first select a face or datum plane via ISelectionService,
/// then call InsertSketch, draw entities, then FinishSketch.
/// All coordinates are in meters (SW native SI units).
/// </summary>
public interface ISketchService
{
    /// <summary>Open a sketch on the currently selected face/plane.</summary>
    void InsertSketch();

    /// <summary>Exit sketch edit mode.</summary>
    void FinishSketch();

    /// <summary>Draw a line from (x1,y1) to (x2,y2) in sketch space (meters).</summary>
    SketchEntityInfo AddLine(double x1, double y1, double x2, double y2);

    /// <summary>Draw a circle centered at (cx,cy) with given radius (meters).</summary>
    SketchEntityInfo AddCircle(double cx, double cy, double radius);

    /// <summary>Draw a corner rectangle from (x1,y1) to (x2,y2) in sketch space (meters).</summary>
    SketchEntityInfo AddRectangle(double x1, double y1, double x2, double y2);

    /// <summary>
    /// Draw an arc with center (cx,cy), start point (x1,y1), end point (x2,y2).
    /// Direction: 1 = counter-clockwise, -1 = clockwise.
    /// </summary>
    SketchEntityInfo AddArc(double cx, double cy, double x1, double y1, double x2, double y2, int direction);
}

/// <summary>
/// Implements <see cref="ISketchService"/> via SolidWorks SketchManager COM API.
/// </summary>
public class SketchService : ISketchService
{
    private readonly ISwConnectionManager _cm;

    public SketchService(ISwConnectionManager cm)
    {
        _cm = cm ?? throw new ArgumentNullException(nameof(cm));
    }

    public void InsertSketch()
    {
        _cm.EnsureConnected();
        GetSketchManager().InsertSketch(true);
    }

    public void FinishSketch()
    {
        _cm.EnsureConnected();
        // InsertSketch(false) closes the sketch and exits edit mode
        GetSketchManager().InsertSketch(false);
    }

    public SketchEntityInfo AddLine(double x1, double y1, double x2, double y2)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var line = skm.CreateLine(x1, y1, 0, x2, y2, 0)
            ?? throw new InvalidOperationException("Failed to create sketch line");
        return new SketchEntityInfo("Line", x1, y1, x2, y2);
    }

    public SketchEntityInfo AddCircle(double cx, double cy, double radius)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var circle = skm.CreateCircleByRadius(cx, cy, 0, radius)
            ?? throw new InvalidOperationException("Failed to create sketch circle");
        return new SketchEntityInfo("Circle", cx, cy, cx + radius, cy);
    }

    public SketchEntityInfo AddRectangle(double x1, double y1, double x2, double y2)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var rect = skm.CreateCornerRectangle(x1, y1, 0, x2, y2, 0)
            ?? throw new InvalidOperationException("Failed to create sketch rectangle");
        return new SketchEntityInfo("Rectangle", x1, y1, x2, y2);
    }

    public SketchEntityInfo AddArc(double cx, double cy, double x1, double y1, double x2, double y2, int direction)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var arc = skm.CreateArc(cx, cy, 0, x1, y1, 0, x2, y2, 0, (short)direction)
            ?? throw new InvalidOperationException("Failed to create sketch arc");
        return new SketchEntityInfo("Arc", cx, cy, x2, y2);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private ISketchManager GetSketchManager()
    {
        return _cm.SwApp!.SketchManager
            ?? throw new InvalidOperationException(
                "No active document. Open a document and select a face/plane first.");
    }
}
