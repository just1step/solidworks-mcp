using ModelContextProtocol.Server;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;
using System.Text.Json;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public class SelectionTools(StaDispatcher sta, ISelectionService selection)
{
    [McpServerTool, Description("Report whether the active document is currently editing a sketch or is otherwise in a safe state for FeatureManager tree reads and delete operations. Use this before ListFeatureTree, DeleteFeatureByName, or DeleteUnusedSketches; if IsEditing is true, finish the sketch first.")]
    public async Task<string> GetEditState()
    {
        var state = await sta.InvokeLoggedAsync(nameof(GetEditState), null, selection.GetEditState);
        return JsonSerializer.Serialize(state);
    }

    [McpServerTool, Description("List the active document's top-level FeatureManager tree items so modeling steps can be verified against the real tree state. This tool only works in non-edit state, so check GetEditState and finish any active sketch first.")]
    public async Task<string> ListFeatureTree()
    {
        var list = await sta.InvokeLoggedAsync(nameof(ListFeatureTree), null, selection.ListFeatureTree);
        return JsonSerializer.Serialize(list);
    }

    [McpServerTool, Description("Read the active document's FeatureManager error state using SolidWorks' official feature error codes and What's Wrong diagnostics. Use this when the SolidWorks UI shows red/error items and you need structured error details instead of inferring problems from screenshots.")]
    public async Task<string> GetFeatureDiagnostics()
    {
        var diagnostics = await sta.InvokeLoggedAsync(nameof(GetFeatureDiagnostics), null, selection.GetFeatureDiagnostics);
        return JsonSerializer.Serialize(diagnostics);
    }

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

    [McpServerTool, Description("List reference planes from the active document feature tree. The returned names are localized to the current SolidWorks language.")]
    public async Task<string> ListReferencePlanes()
    {
        var planes = await sta.InvokeLoggedAsync(nameof(ListReferencePlanes), null, selection.ListReferencePlanes);
        return JsonSerializer.Serialize(planes);
    }

    [McpServerTool, Description("Get the current SolidWorks UI language plus the active document's localized reference plane snapshot.")]
    public async Task<string> GetSolidWorksContext()
    {
        var context = await sta.InvokeLoggedAsync(nameof(GetSolidWorksContext), null, selection.GetSolidWorksContext);
        return JsonSerializer.Serialize(context);
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

    [McpServerTool, Description("Measure two topology entities using SolidWorks' official IMeasure API. Provide indexes from ListEntities; the tool will select both entities internally, call IModelDocExtension.CreateMeasure().Calculate(null), and return distance-related values.")]
    public async Task<string> MeasureEntities(
        [Description("First entity type: Face, Edge, or Vertex")] string firstEntityType,
        [Description("Zero-based index of the first entity from ListEntities")] int firstIndex,
        [Description("Second entity type: Face, Edge, or Vertex")] string secondEntityType,
        [Description("Zero-based index of the second entity from ListEntities")] int secondIndex,
        [Description("Optional component name for the first entity in assembly context. Leave null for part context or top-level.")] string? firstComponentName = null,
        [Description("Optional component name for the second entity in assembly context. Leave null for part context or top-level.")] string? secondComponentName = null,
        [Description("Arc/circle measurement mode: 0=center, 1=minimum distance, 2=maximum distance.")] int arcOption = 1)
    {
        var firstType = Enum.Parse<SelectableEntityType>(firstEntityType, ignoreCase: true);
        var secondType = Enum.Parse<SelectableEntityType>(secondEntityType, ignoreCase: true);
        var payload = new
        {
            firstEntityType = firstType.ToString(),
            firstIndex,
            secondEntityType = secondType.ToString(),
            secondIndex,
            firstComponentName,
            secondComponentName,
            arcOption,
        };
        var result = await sta.InvokeLoggedAsync(
            nameof(MeasureEntities),
            payload,
            () => selection.MeasureEntities(firstType, firstIndex, secondType, secondIndex, firstComponentName, secondComponentName, arcOption));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Delete a top-level FeatureManager item by its exact feature-tree name. This is only valid in non-edit state; call GetEditState first and finish any active sketch before deleting.")]
    public async Task<string> DeleteFeatureByName(
        [Description("Exact feature-tree name, e.g. Sketch3 or Cut-Extrude2")] string featureName)
    {
        var result = await sta.InvokeLoggedAsync(nameof(DeleteFeatureByName), new { featureName }, () => selection.DeleteFeatureByName(featureName));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Delete loose sketches that exist in the FeatureManager tree but are not consumed by downstream features. This is only valid in non-edit state; call GetEditState first and finish any active sketch before cleanup.")]
    public async Task<string> DeleteUnusedSketches()
    {
        var result = await sta.InvokeLoggedAsync(nameof(DeleteUnusedSketches), null, selection.DeleteUnusedSketches);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Clear the current selection set in SolidWorks.")]
    public async Task<string> ClearSelection()
    {
        await sta.InvokeLoggedAsync(nameof(ClearSelection), null, selection.ClearSelection);
        return "Selection cleared.";
    }
}
