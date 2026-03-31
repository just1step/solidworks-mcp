using ModelContextProtocol.Server;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;
using System.Text.Json;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public class DocumentTools(StaDispatcher sta, IDocumentService docs)
{
    [McpServerTool, Description("Create a new SolidWorks document. type: 'Part', 'Assembly', or 'Drawing'.")]
    public async Task<string> NewDocument(
        [Description("Document type: Part, Assembly, or Drawing")] string type = "Part",
        [Description("Optional path to a template file (.prtdot, .asmdot, .drwdot)")] string? templatePath = null)
    {
        var docType = ParseDocType(type);
        var info = await sta.InvokeAsync(() => docs.NewDocument(docType, templatePath));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Open an existing SolidWorks document by file path.")]
    public async Task<string> OpenDocument(
        [Description("Full file path to the SolidWorks document")] string path)
    {
        var info = await sta.InvokeAsync(() => docs.OpenDocument(path));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Close an open SolidWorks document by file path.")]
    public async Task<string> CloseDocument(
        [Description("Full file path of the document to close")] string path)
    {
        await sta.InvokeAsync(() => docs.CloseDocument(path));
        return $"Closed: {path}";
    }

    [McpServerTool, Description("Save an open SolidWorks document by file path.")]
    public async Task<string> SaveDocument(
        [Description("Full file path of the document to save")] string path)
    {
        await sta.InvokeAsync(() => docs.SaveDocument(path));
        return $"Saved: {path}";
    }

    [McpServerTool, Description("Save or export a SolidWorks document to a new file path. The output format is inferred from the extension, so this supports native SolidWorks formats and common exports like STEP or STL. When sourcePath is omitted, the active document is used.")]
    public async Task<string> SaveDocumentAs(
        [Description("Output file path. Extension determines the saved/exported format.")] string outputPath,
        [Description("Optional source document path. Defaults to the active document when omitted.")] string? sourcePath = null,
        [Description("When true, performs a copy/export and keeps the source document path unchanged.")] bool saveAsCopy = true)
    {
        var result = await sta.InvokeAsync(() => docs.SaveDocumentAs(outputPath, sourcePath, saveAsCopy));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Undo the last N operations on the active SolidWorks document.")]
    public async Task<string> Undo(
        [Description("Number of undo steps to apply.")] int steps = 1)
    {
        await sta.InvokeAsync(() => docs.Undo(steps));
        return $"Undid {steps} step(s).";
    }

    [McpServerTool, Description("Switch the active SolidWorks document to a standard view. Supported values: front, back, left, right, top, bottom, isometric, trimetric, dimetric.")]
    public async Task<string> ShowStandardView(
        [Description("Standard view name.")] string view = "isometric")
    {
        var standardView = ParseStandardView(view);
        await sta.InvokeAsync(() => docs.ShowStandardView(standardView));
        return $"Switched to {standardView} view.";
    }

    [McpServerTool, Description("Rotate the active SolidWorks view around the global x, y, and z axes. Angles are in degrees.")]
    public async Task<string> RotateView(
        [Description("Rotation around the global X axis in degrees.")] double xDegrees = 0,
        [Description("Rotation around the global Y axis in degrees.")] double yDegrees = 0,
        [Description("Rotation around the global Z axis in degrees.")] double zDegrees = 0)
    {
        await sta.InvokeAsync(() => docs.RotateView(xDegrees, yDegrees, zDegrees));
        return JsonSerializer.Serialize(new { xDegrees, yDegrees, zDegrees });
    }

    [McpServerTool, Description("Export the current SolidWorks viewport to a PNG file.")]
    public async Task<string> ExportCurrentViewPng(
        [Description("Output PNG file path.")] string outputPath,
        [Description("Image width in pixels.")] int width = 1600,
        [Description("Image height in pixels.")] int height = 900,
        [Description("When true, includes the PNG as base64 data in the tool result.")] bool includeBase64Data = false)
    {
        var result = await sta.InvokeAsync(() => docs.ExportCurrentViewPng(outputPath, width, height, includeBase64Data));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("List all currently open SolidWorks documents.")]
    public async Task<string> ListDocuments()
    {
        var list = await sta.InvokeAsync(docs.ListDocuments);
        return JsonSerializer.Serialize(list);
    }

    [McpServerTool, Description("Get the currently active SolidWorks document.")]
    public async Task<string> GetActiveDocument()
    {
        var info = await sta.InvokeAsync(docs.GetActiveDocument);
        return info is null ? "null" : JsonSerializer.Serialize(info);
    }

    private static SwDocType ParseDocType(string type) => type.ToLowerInvariant() switch
    {
        "assembly" or "asm" => SwDocType.Assembly,
        "drawing" or "drw"  => SwDocType.Drawing,
        _                   => SwDocType.Part,
    };

    private static SwStandardView ParseStandardView(string view) => view.ToLowerInvariant() switch
    {
        "front" => SwStandardView.Front,
        "back" => SwStandardView.Back,
        "left" => SwStandardView.Left,
        "right" => SwStandardView.Right,
        "top" => SwStandardView.Top,
        "bottom" => SwStandardView.Bottom,
        "iso" or "isometric" => SwStandardView.Isometric,
        "trimetric" => SwStandardView.Trimetric,
        "dimetric" => SwStandardView.Dimetric,
        _ => throw new ArgumentException($"Unknown standard view: '{view}'.")
    };
}
