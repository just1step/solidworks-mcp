using ModelContextProtocol.Server;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;
using System.Text.Json;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public class SketchTools(StaDispatcher sta, ISketchService sketch)
{
    [McpServerTool, Description("Open a new sketch on the currently selected datum plane or planar face. This is only valid after exactly one planar face or datum plane has already been selected.")]
    public async Task<string> InsertSketch()
    {
        await sta.InvokeLoggedAsync(nameof(InsertSketch), null, sketch.InsertSketch);
        return "Sketch edit mode started.";
    }

    [McpServerTool, Description("Close (finish) the currently open sketch and return to 3D mode.")]
    public async Task<string> FinishSketch()
    {
        await sta.InvokeLoggedAsync(nameof(FinishSketch), null, sketch.FinishSketch);
        return "Sketch finished.";
    }

    [McpServerTool, Description("Project the currently selected edges or loops into the active sketch using SolidWorks' ISketchManager.SketchUseEdge3 API. This requires an active sketch and a selected set of edges before calling it; selecting only a face is not sufficient. For the common face-outline cut workflow, prefer CutFaceByProjectedEdges so the face-to-edges conversion and cut sequencing are handled automatically.")]
    public async Task<string> SketchUseEdge3(
        [Description("True to project the full contiguous chain; false to project only the currently selected items.")] bool chain = false,
        [Description("True to convert inner loops too, such as holes or pockets inside a selected face.")] bool innerLoops = true)
    {
        await sta.InvokeLoggedAsync(nameof(SketchUseEdge3), new { chain, innerLoops }, () => sketch.SketchUseEdge3(chain, innerLoops));
        return "Projected the selected edges into sketch geometry.";
    }

    [McpServerTool, Description("Draw a point in the active sketch. Coordinates in meters.")]
    public async Task<string> AddPoint(
        [Description("X coordinate (meters)")] double x,
        [Description("Y coordinate (meters)")] double y)
    {
        var info = await sta.InvokeLoggedAsync(nameof(AddPoint), new { x, y }, () => sketch.AddPoint(x, y));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Draw a line in the active sketch. Coordinates in meters.")]
    public async Task<string> AddLine(
        [Description("Start X (meters)")] double x1,
        [Description("Start Y (meters)")] double y1,
        [Description("End X (meters)")] double x2,
        [Description("End Y (meters)")] double y2)
    {
        var info = await sta.InvokeLoggedAsync(nameof(AddLine), new { x1, y1, x2, y2 }, () => sketch.AddLine(x1, y1, x2, y2));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Draw a circle in the active sketch. Coordinates and radius in meters.")]
    public async Task<string> AddCircle(
        [Description("Center X (meters)")] double cx,
        [Description("Center Y (meters)")] double cy,
        [Description("Radius (meters)")] double radius)
    {
        var info = await sta.InvokeLoggedAsync(nameof(AddCircle), new { cx, cy, radius }, () => sketch.AddCircle(cx, cy, radius));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Draw a corner rectangle in the active sketch. Coordinates in meters.")]
    public async Task<string> AddRectangle(
        [Description("First corner X (meters)")] double x1,
        [Description("First corner Y (meters)")] double y1,
        [Description("Opposite corner X (meters)")] double x2,
        [Description("Opposite corner Y (meters)")] double y2)
    {
        var info = await sta.InvokeLoggedAsync(nameof(AddRectangle), new { x1, y1, x2, y2 }, () => sketch.AddRectangle(x1, y1, x2, y2));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Draw an arc in the active sketch. Coordinates in meters. Direction: 1=CCW, -1=CW.")]
    public async Task<string> AddArc(
        [Description("Center X (meters)")] double cx,
        [Description("Center Y (meters)")] double cy,
        [Description("Start point X (meters)")] double x1,
        [Description("Start point Y (meters)")] double y1,
        [Description("End point X (meters)")] double x2,
        [Description("End point Y (meters)")] double y2,
        [Description("Arc direction: 1 = counter-clockwise, -1 = clockwise")] int direction = 1)
    {
        var info = await sta.InvokeLoggedAsync(nameof(AddArc), new { cx, cy, x1, y1, x2, y2, direction }, () => sketch.AddArc(cx, cy, x1, y1, x2, y2, direction));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Draw an ellipse in the active sketch. All coordinates in meters.")]
    public async Task<string> AddEllipse(
        [Description("Center X (meters)")] double cx,
        [Description("Center Y (meters)")] double cy,
        [Description("Major-axis point X (meters)")] double majorX,
        [Description("Major-axis point Y (meters)")] double majorY,
        [Description("Minor-axis point X (meters)")] double minorX,
        [Description("Minor-axis point Y (meters)")] double minorY)
    {
        var info = await sta.InvokeLoggedAsync(nameof(AddEllipse), new { cx, cy, majorX, majorY, minorX, minorY }, () => sketch.AddEllipse(cx, cy, majorX, majorY, minorX, minorY));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Draw a regular polygon in the active sketch. Coordinates in meters.")]
    public async Task<string> AddPolygon(
        [Description("Center X (meters)")] double cx,
        [Description("Center Y (meters)")] double cy,
        [Description("A point on the perimeter X (meters)")] double x,
        [Description("A point on the perimeter Y (meters)")] double y,
        [Description("Number of sides (minimum 3)")] int sides,
        [Description("True = inscribed (vertex on circle), False = circumscribed (edge tangent to circle)")] bool inscribed = true)
    {
        var info = await sta.InvokeLoggedAsync(nameof(AddPolygon), new { cx, cy, x, y, sides, inscribed }, () => sketch.AddPolygon(cx, cy, x, y, sides, inscribed));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Insert sketch text at the given position in the active sketch.")]
    public async Task<string> AddText(
        [Description("Text anchor X (meters)")] double x,
        [Description("Text anchor Y (meters)")] double y,
        [Description("Text content to insert")] string text,
        [Description("Optional text justification: left, center, right, or fullyJustified.")] string? justification = null,
        [Description("Optional: flip the text direction relative to the sketch x-axis.")] bool flipDirection = false,
        [Description("Optional: mirror the text horizontally.")] bool horizontalMirror = false,
        [Description("Optional character height in meters.")] double? height = null,
        [Description("Optional font family name, for example 'Arial' or 'Century Gothic'.")] string? fontName = null,
        [Description("Optional bold toggle.")] bool? bold = null,
        [Description("Optional italic toggle.")] bool? italic = null,
        [Description("Optional underline toggle.")] bool? underline = null,
        [Description("Optional width factor multiplier. 1.0 keeps default width.")] double? widthFactor = null,
        [Description("Optional character spacing multiplier. 1.0 keeps default spacing.")] double? charSpacingFactor = null,
        [Description("Optional text rotation in degrees.")] double? rotationDegrees = null)
    {
        var info = await sta.InvokeLoggedAsync(
            nameof(AddText),
            new { x, y, text, justification, flipDirection, horizontalMirror, height, fontName, bold, italic, underline, widthFactor, charSpacingFactor, rotationDegrees },
            () =>
            {
                var options = new SketchTextOptions
                {
                    Justification = ToolArgumentParsing.ParseSketchTextJustification(justification),
                    FlipDirection = flipDirection,
                    HorizontalMirror = horizontalMirror,
                    Height = height,
                    FontName = fontName,
                    Bold = bold,
                    Italic = italic,
                    Underline = underline,
                    WidthFactor = widthFactor,
                    CharSpacingFactor = charSpacingFactor,
                    RotationDegrees = rotationDegrees,
                };

                return sketch.AddText(x, y, text, options);
            });
        return JsonSerializer.Serialize(info);
    }
}
