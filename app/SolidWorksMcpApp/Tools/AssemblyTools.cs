using ModelContextProtocol.Server;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;
using System.Text.Json;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public class AssemblyTools(StaDispatcher sta, IAssemblyService assembly)
{
    [McpServerTool, Description("Insert a component into the active SolidWorks assembly at the given position.")]
    public async Task<string> InsertComponent(
        [Description("Full file path to the component (.sldprt or .sldasm)")] string filePath,
        [Description("X position in meters (default 0)")] double x = 0,
        [Description("Y position in meters (default 0)")] double y = 0,
        [Description("Z position in meters (default 0)")] double z = 0)
    {
        var info = await sta.InvokeLoggedAsync(nameof(InsertComponent), new { filePath, x, y, z }, () => assembly.InsertComponent(filePath, x, y, z));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Add a Coincident mate between the two currently-selected faces, edges, or planes.")]
    public async Task<string> AddMateCoincident(
        [Description("Mate alignment: None=0, AntiAligned=1, Closest=2")] int align = 2)
    {
        var result = await sta.InvokeLoggedAsync(nameof(AddMateCoincident), new { align }, () => assembly.AddMateCoincident((MateAlign)align));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Add a Concentric mate between the two currently-selected cylindrical faces or circular edges.")]
    public async Task<string> AddMateConcentric(
        [Description("Mate alignment: None=0, AntiAligned=1, Closest=2")] int align = 2)
    {
        var result = await sta.InvokeLoggedAsync(nameof(AddMateConcentric), new { align }, () => assembly.AddMateConcentric((MateAlign)align));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Add a Parallel mate between the two currently-selected planar faces or edges.")]
    public async Task<string> AddMateParallel(
        [Description("Mate alignment: None=0, AntiAligned=1, Closest=2")] int align = 2)
    {
        var result = await sta.InvokeLoggedAsync(nameof(AddMateParallel), new { align }, () => assembly.AddMateParallel((MateAlign)align));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Add a Distance mate between the two currently-selected entities.")]
    public async Task<string> AddMateDistance(
        [Description("Distance between the entities in meters")] double distance,
        [Description("Mate alignment: None=0, AntiAligned=1, Closest=2")] int align = 2)
    {
        var result = await sta.InvokeLoggedAsync(nameof(AddMateDistance), new { distance, align }, () => assembly.AddMateDistance(distance, (MateAlign)align));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Add an Angle mate between the two currently-selected planar entities.")]
    public async Task<string> AddMateAngle(
        [Description("Angle between the entities in degrees")] double angleDegrees,
        [Description("Mate alignment: None=0, AntiAligned=1, Closest=2")] int align = 2)
    {
        var result = await sta.InvokeLoggedAsync(nameof(AddMateAngle), new { angleDegrees, align }, () => assembly.AddMateAngle(angleDegrees, (MateAlign)align));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("List all components in the active SolidWorks assembly.")]
    public async Task<string> ListComponents()
    {
        var list = await sta.InvokeLoggedAsync(nameof(ListComponents), null, assembly.ListComponents);
        return JsonSerializer.Serialize(list);
    }

    [McpServerTool, Description("List all component instances in the active SolidWorks assembly by recursively traversing subassemblies. Returns each instance with its hierarchy path and depth so shared parts can be counted before editing.")]
    public async Task<string> ListComponentsRecursive()
    {
        var list = await sta.InvokeLoggedAsync(nameof(ListComponentsRecursive), null, assembly.ListComponentsRecursive);
        return JsonSerializer.Serialize(list);
    }

    [McpServerTool, Description("Resolve one exact component instance in the active SolidWorks assembly by name, hierarchy path, path, or any combination. Returns a resolved target or explicit ambiguity details for downstream workflows.")]
    public async Task<string> ResolveComponentTarget(
        [Description("Optional component instance name to match exactly, for example Pulley-1.")] string? componentName = null,
        [Description("Optional full hierarchy path to match exactly. This is the most precise selector when available.")] string? hierarchyPath = null,
        [Description("Optional full source model path to match exactly.")] string? componentPath = null)
    {
        var payload = new { componentName, hierarchyPath, componentPath };
        var result = await sta.InvokeLoggedAsync(nameof(ResolveComponentTarget), payload, () => assembly.ResolveComponentTarget(componentName, hierarchyPath, componentPath));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Analyze how many placements would change if the resolved component's source file were edited directly. Use this before any geometry-changing part edit.")]
    public async Task<string> AnalyzeSharedPartEditImpact(
        [Description("Optional component instance name to match exactly, for example Pulley-1.")] string? componentName = null,
        [Description("Optional full hierarchy path to match exactly. This is the most precise selector when available.")] string? hierarchyPath = null,
        [Description("Optional full source model path to match exactly.")] string? componentPath = null)
    {
        var payload = new { componentName, hierarchyPath, componentPath };
        var result = await sta.InvokeLoggedAsync(nameof(AnalyzeSharedPartEditImpact), payload, () => assembly.AnalyzeSharedPartEditImpact(componentName, hierarchyPath, componentPath));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Run a static interference check in the active SolidWorks assembly using the official ToolsCheckInterference2 workflow. The assembly must be fully resolved, and this tool temporarily changes the selection set while it runs.")]
    public async Task<string> CheckInterference(
        [Description("Optional component instance hierarchy paths to check. Leave null to check the whole active assembly.")] string[]? hierarchyPaths = null,
        [Description("When true, coincident or touching faces are treated as interference.")] bool treatCoincidenceAsInterference = false)
    {
        var payload = new { hierarchyPaths, treatCoincidenceAsInterference };
        var result = await sta.InvokeLoggedAsync(nameof(CheckInterference), payload, () => assembly.CheckInterference(hierarchyPaths, treatCoincidenceAsInterference));
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Replace a top-level component instance in the active SolidWorks assembly with another model file. For subassembly content, open the owning subassembly first.")]
    public async Task<string> ReplaceComponent(
        [Description("Hierarchy path of the component instance to replace. The target must be top-level in the active assembly.")] string hierarchyPath,
        [Description("Full file path to the replacement part or assembly.")] string replacementFilePath,
        [Description("Optional configuration name in the replacement model. Empty string uses the default or matched configuration.")] string configName = "",
        [Description("When true, replace all instances of the selected component in the active assembly.")] bool replaceAllInstances = false,
        [Description("Configuration choice: MatchName=0, ManuallySelect=1.")] int useConfigChoice = 0,
        [Description("When true, SolidWorks attempts to reattach existing mates to the replacement component.")] bool reattachMates = true)
    {
        var payload = new { hierarchyPath, replacementFilePath, configName, replaceAllInstances, useConfigChoice, reattachMates };
        var result = await sta.InvokeLoggedAsync(nameof(ReplaceComponent), payload, () => assembly.ReplaceComponent(hierarchyPath, replacementFilePath, configName, replaceAllInstances, useConfigChoice, reattachMates));
        return JsonSerializer.Serialize(result);
    }

}
