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
}
