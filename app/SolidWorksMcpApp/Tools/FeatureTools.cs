using ModelContextProtocol.Server;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;
using System.Text.Json;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public class FeatureTools(StaDispatcher sta, IFeatureService feature)
{
    [McpServerTool, Description("Extrude the active sketch profile to create a boss/base feature.")]
    public async Task<string> Extrude(
        [Description("Extrusion depth in meters")] double depth,
        [Description("End condition: Blind=0, ThroughAll=1, MidPlane=6")] int endCondition = 0,
        [Description("Flip the extrusion direction")] bool flipDirection = false)
    {
        var info = await sta.InvokeAsync(() => feature.Extrude(depth, (EndCondition)endCondition, flipDirection));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Extrude-cut the active sketch profile to remove material.")]
    public async Task<string> ExtrudeCut(
        [Description("Cut depth in meters")] double depth,
        [Description("End condition: Blind=0, ThroughAll=1, MidPlane=6")] int endCondition = 0,
        [Description("Flip the cut direction")] bool flipDirection = false)
    {
        var info = await sta.InvokeAsync(() => feature.ExtrudeCut(depth, (EndCondition)endCondition, flipDirection));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Revolve the active sketch profile around the selected axis.")]
    public async Task<string> Revolve(
        [Description("Revolve angle in degrees (0-360)")] double angleDegrees,
        [Description("True to create a cut revolve; False for a boss revolve")] bool isCut = false)
    {
        var info = await sta.InvokeAsync(() => feature.Revolve(angleDegrees, isCut));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Apply a fillet to the currently selected edges.")]
    public async Task<string> Fillet(
        [Description("Fillet radius in meters")] double radius)
    {
        var info = await sta.InvokeAsync(() => feature.Fillet(radius));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Apply a chamfer to the currently selected edges.")]
    public async Task<string> Chamfer(
        [Description("Chamfer distance in meters")] double distance)
    {
        var info = await sta.InvokeAsync(() => feature.Chamfer(distance));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Shell the active solid: hollow it out by removing selected faces and applying a wall thickness.")]
    public async Task<string> Shell(
        [Description("Shell wall thickness in meters")] double thickness)
    {
        var info = await sta.InvokeAsync(() => feature.Shell(thickness));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Create a simple hole at the selected point on a face.")]
    public async Task<string> SimpleHole(
        [Description("Hole diameter in meters")] double diameter,
        [Description("Hole depth in meters")] double depth,
        [Description("End condition: Blind=0, ThroughAll=1, MidPlane=6")] int endCondition = 0)
    {
        var info = await sta.InvokeAsync(() => feature.SimpleHole(diameter, depth, (EndCondition)endCondition));
        return JsonSerializer.Serialize(info);
    }
}
