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
}
