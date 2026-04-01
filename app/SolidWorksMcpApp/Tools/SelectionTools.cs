using ModelContextProtocol.Server;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;
using System.Text.Json;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public class SelectionTools(StaDispatcher sta, ISelectionService selection)
{
    [McpServerTool, Description("Select an entity in SolidWorks by name and selection type string. For datum planes, prefer 'PLANE'; common SolidWorks enum names like 'swSelDATUMPLANES' are also accepted and retried with compatible aliases.")]
    public async Task<string> SelectByName(
        [Description("Name of the entity to select")] string name,
        [Description("SolidWorks selection type string, e.g. 'swSelDATUMPLANES', 'swSelFACES'")] string selType)
    {
        var result = await sta.InvokeLoggedAsync(nameof(SelectByName), new { name, selType }, () => selection.SelectByName(name, selType));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("List selectable topology entities (Face, Edge, Vertex) on the active document.")]
    public async Task<string> ListEntities(
        [Description("Filter by entity type: Face, Edge, or Vertex. Leave null for all.")] string? entityType = null,
        [Description("Filter by component name in assembly context. Leave null for top-level.")] string? componentName = null)
    {
        var type = entityType is null
            ? (SelectableEntityType?)null
            : Enum.Parse<SelectableEntityType>(entityType, ignoreCase: true);
        var list = await sta.InvokeLoggedAsync(nameof(ListEntities), new { entityType = type?.ToString(), componentName }, () => selection.ListEntities(type, componentName));
        return JsonSerializer.Serialize(list);
    }

    [McpServerTool, Description("Select a topology entity by index (from ListEntities). Needed before sketch or feature operations on a face.")]
    public async Task<string> SelectEntity(
        [Description("Entity type: Face, Edge, or Vertex")] string entityType,
        [Description("Zero-based index from ListEntities")] int index,
        [Description("Append to current selection instead of replacing it")] bool append = false,
        [Description("Selection mark value (default 0)")] int mark = 0,
        [Description("Component name for assembly context. Leave null for top-level.")] string? componentName = null)
    {
        var type = Enum.Parse<SelectableEntityType>(entityType, ignoreCase: true);
        var result = await sta.InvokeLoggedAsync(nameof(SelectEntity), new { entityType = type.ToString(), index, append, mark, componentName }, () => selection.SelectEntity(type, index, append, mark, componentName));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Clear the current selection set in SolidWorks.")]
    public async Task<string> ClearSelection()
    {
        await sta.InvokeLoggedAsync(nameof(ClearSelection), null, selection.ClearSelection);
        return "Selection cleared.";
    }
}
