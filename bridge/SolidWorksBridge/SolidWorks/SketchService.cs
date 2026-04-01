using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

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

    /// <summary>Draw a point at (x,y) in sketch space (meters).</summary>
    SketchEntityInfo AddPoint(double x, double y);

    /// <summary>Draw an ellipse using center, major-axis point, and minor-axis point coordinates.</summary>
    SketchEntityInfo AddEllipse(double cx, double cy, double majorX, double majorY, double minorX, double minorY);

    /// <summary>Draw a regular polygon using center, one perimeter point, side count, and inscribed mode.</summary>
    SketchEntityInfo AddPolygon(double cx, double cy, double x, double y, int sides, bool inscribed);

    /// <summary>Insert sketch text at (x,y) using SolidWorks default text formatting.</summary>
    SketchEntityInfo AddText(double x, double y, string text);

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
        var doc = GetModelDoc();
        ValidateSketchHostSelection(doc);
        GetSketchManager().InsertSketch(true);

        if (doc.GetActiveSketch2() == null)
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.InsertSketch",
                "SolidWorks did not enter sketch edit mode. Ensure a plane or planar face is selected before inserting a sketch.");
        }
    }

    public void FinishSketch()
    {
        _cm.EnsureConnected();
        var doc = _cm.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException(
                "No active document. Open a document and select a face/plane first.");
        doc.ClearSelection2(true);
        GetSketchManager().InsertSketch(true);

        if (doc.GetActiveSketch2() != null)
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.InsertSketch",
                "SolidWorks remained in sketch edit mode after FinishSketch.");
        }
    }

    public SketchEntityInfo AddPoint(double x, double y)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var point = skm.CreatePoint(x, y, 0)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.CreatePoint",
                "SolidWorks did not create the sketch point.",
                new Dictionary<string, object?> { ["x"] = x, ["y"] = y });
        return new SketchEntityInfo("Point", x, y, x, y);
    }

    public SketchEntityInfo AddEllipse(double cx, double cy, double majorX, double majorY, double minorX, double minorY)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var ellipse = skm.CreateEllipse(cx, cy, 0, majorX, majorY, 0, minorX, minorY, 0)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.CreateEllipse",
                "SolidWorks did not create the sketch ellipse.");
        return new SketchEntityInfo("Ellipse", cx, cy, majorX, majorY);
    }

    public SketchEntityInfo AddPolygon(double cx, double cy, double x, double y, int sides, bool inscribed)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var polygon = skm.CreatePolygon(cx, cy, 0, x, y, 0, sides, inscribed)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.CreatePolygon",
                "SolidWorks did not create the sketch polygon.");
        return new SketchEntityInfo("Polygon", cx, cy, x, y);
    }

    public SketchEntityInfo AddText(double x, double y, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("text must not be empty", nameof(text));
        }

        _cm.EnsureConnected();
        var doc = _cm.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException(
                "No active document. Open a document and select a face/plane first.");
        var sketchText = doc.InsertSketchText(x, y, 0, text, 0, 0, 0, 100, 100)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "IModelDoc2.InsertSketchText",
                "SolidWorks did not create sketch text.");
        return new SketchEntityInfo("Text", x, y, x, y);
    }

    public SketchEntityInfo AddLine(double x1, double y1, double x2, double y2)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var line = skm.CreateLine(x1, y1, 0, x2, y2, 0)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.CreateLine",
                "SolidWorks did not create the sketch line.");
        return new SketchEntityInfo("Line", x1, y1, x2, y2);
    }

    public SketchEntityInfo AddCircle(double cx, double cy, double radius)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var circle = skm.CreateCircleByRadius(cx, cy, 0, radius)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.CreateCircleByRadius",
                "SolidWorks did not create the sketch circle.",
                new Dictionary<string, object?> { ["cx"] = cx, ["cy"] = cy, ["radius"] = radius });
        return new SketchEntityInfo("Circle", cx, cy, cx + radius, cy);
    }

    public SketchEntityInfo AddRectangle(double x1, double y1, double x2, double y2)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var rect = skm.CreateCornerRectangle(x1, y1, 0, x2, y2, 0)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.CreateCornerRectangle",
                "SolidWorks did not create the sketch rectangle.");
        return new SketchEntityInfo("Rectangle", x1, y1, x2, y2);
    }

    public SketchEntityInfo AddArc(double cx, double cy, double x1, double y1, double x2, double y2, int direction)
    {
        _cm.EnsureConnected();
        var skm = GetSketchManager();
        var arc = skm.CreateArc(cx, cy, 0, x1, y1, 0, x2, y2, 0, (short)direction)
            ?? throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.CreateArc",
                "SolidWorks did not create the sketch arc.");
        return new SketchEntityInfo("Arc", cx, cy, x2, y2);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private ISketchManager GetSketchManager()
    {
        return _cm.SwApp!.SketchManager
            ?? throw new InvalidOperationException(
                "No active document. Open a document and select a face/plane first.");
    }

    private IModelDoc2 GetModelDoc()
    {
        return _cm.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException(
                "No active document. Open a document and select a face/plane first.");
    }

    private static void ValidateSketchHostSelection(IModelDoc2 doc)
    {
        var selectionManager = doc.ISelectionManager;
        if (selectionManager == null)
        {
            return;
        }

        int selectedCount = selectionManager.GetSelectedObjectCount2(-1);
        if (selectedCount != 1)
        {
            throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.InsertSketch",
                "InsertSketch requires exactly one selected planar face or datum plane.",
                new Dictionary<string, object?>
                {
                    ["selectedCount"] = selectedCount,
                });
        }

        int selectedType = selectionManager.GetSelectedObjectType3(1, -1);
        if (selectedType == (int)swSelectType_e.swSelDATUMPLANES)
        {
            return;
        }

        if (selectedType == (int)swSelectType_e.swSelFACES)
        {
            var face = selectionManager.GetSelectedObject6(1, -1) as IFace2;
            if (face == null)
            {
                throw SolidWorksApiErrorFactory.FromValidationFailure(
                    "ISketchManager.InsertSketch",
                    "SolidWorks reported a selected face, but the face object could not be resolved.");
            }

            var surface = face.GetSurface() as ISurface;
            if (surface == null)
            {
                throw SolidWorksApiErrorFactory.FromValidationFailure(
                    "ISketchManager.InsertSketch",
                    "SolidWorks reported a selected face, but the face surface could not be resolved.");
            }

            if (surface.IsPlane())
            {
                return;
            }

            throw SolidWorksApiErrorFactory.FromValidationFailure(
                "ISketchManager.InsertSketch",
                "InsertSketch can only start on a planar face or datum plane, but the selected face is not planar.",
                new Dictionary<string, object?>
                {
                    ["selectedType"] = Enum.GetName(typeof(swSelectType_e), selectedType) ?? selectedType.ToString(),
                    ["surfaceIdentity"] = surface.Identity(),
                });
        }

        throw SolidWorksApiErrorFactory.FromValidationFailure(
            "ISketchManager.InsertSketch",
            "InsertSketch requires a selected planar face or datum plane.",
            new Dictionary<string, object?>
            {
                ["selectedType"] = Enum.GetName(typeof(swSelectType_e), selectedType) ?? selectedType.ToString(),
            });
    }

}
