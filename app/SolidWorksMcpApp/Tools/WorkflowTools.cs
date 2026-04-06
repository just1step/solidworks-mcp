using ModelContextProtocol.Server;
using SolidWorks.Interop.sldworks;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace SolidWorksMcpApp.Tools;

[McpServerToolType]
public class WorkflowTools(
    StaDispatcher sta,
    ISwConnectionManager connectionManager,
    ISelectionService selection,
    ISketchService sketch,
    IFeatureService feature,
    IWorkflowService workflow)
{
    [McpServerTool, Description("Run the proven one-shot face-cut workflow: select a planar face by ListEntities index, open a sketch on that face, enumerate the face edges via IFace2.GetEdges, select those edges, project them with ISketchManager.SketchUseEdge3, then exit/reselect the sketch as required and create a cut. Use this instead of manually chaining SelectEntity + InsertSketch + SketchUseEdge3 + ExtrudeCut when cutting from an existing face outline.")]
    public async Task<string> CutFaceByProjectedEdges(
        [Description("Zero-based face index from ListEntities(Face).")]
        int faceIndex,
        [Description("Cut depth in meters.")]
        double depth,
        [Description("Cut direction flag passed to the cut workflow.")]
        bool flipDirection = false,
        [Description("True to project inner loops too, such as holes or pockets inside the selected face.")]
        bool innerLoops = true)
    {
        var info = await sta.InvokeLoggedAsync(
            nameof(CutFaceByProjectedEdges),
            new { faceIndex, depth, flipDirection, innerLoops },
            () => CutFaceByProjectedEdgesCore(faceIndex, depth, flipDirection, innerLoops));
        return JsonSerializer.Serialize(info);
    }

    [McpServerTool, Description("Resolve a nested component in the active parent assembly, open the owning subassembly, replace the direct child there, save, reopen the parent assembly, and verify the replacement persisted. The result also includes pre- and post-replacement shared-part impact analysis so edit-safety state is explicit.")]
    public async Task<string> ReplaceNestedComponentAndVerifyPersistence(
        [Description("Full file path to the replacement part or subassembly.")] string replacementFilePath,
        [Description("Optional component instance name to match exactly, for example Pulley-1.")] string? componentName = null,
        [Description("Optional full hierarchy path to match exactly. This is the most precise selector when available.")] string? hierarchyPath = null,
        [Description("Optional full source model path to match exactly.")] string? componentPath = null,
        [Description("Optional configuration name in the replacement model. Empty string uses the default or matched configuration.")] string configName = "",
        [Description("Configuration choice: MatchName=0, ManuallySelect=1.")] int useConfigChoice = 0,
        [Description("When true, SolidWorks attempts to reattach existing mates to the replacement component.")] bool reattachMates = true)
    {
        var payload = new { replacementFilePath, componentName, hierarchyPath, componentPath, configName, useConfigChoice, reattachMates };
        var result = await sta.InvokeLoggedAsync(
            nameof(ReplaceNestedComponentAndVerifyPersistence),
            payload,
            () => workflow.ReplaceNestedComponentAndVerifyPersistence(
                replacementFilePath,
                componentName,
                hierarchyPath,
                componentPath,
                configName,
                useConfigChoice,
                reattachMates));
        return JsonSerializer.Serialize(result);
    }

    private object CutFaceByProjectedEdgesCore(int faceIndex, double depth, bool flipDirection, bool innerLoops)
    {
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), "depth must be positive.");
        }

        connectionManager.EnsureConnected();
        var doc = connectionManager.SwApp!.IActiveDoc2
            ?? throw new InvalidOperationException("No active document.");
        if (doc is not IPartDoc)
        {
            throw new InvalidOperationException("CutFaceByProjectedEdges requires an active part document.");
        }

        doc.ClearSelection2(true);
        var faceSelection = selection.SelectEntity(SelectableEntityType.Face, faceIndex);
        if (!faceSelection.Success)
        {
            throw new InvalidOperationException(faceSelection.Message);
        }

        var face = doc.ISelectionManager?.GetSelectedObject6(1, -1) as IFace2
            ?? throw new InvalidOperationException("Failed to resolve the selected face before opening sketch mode.");

        sketch.InsertSketch();

        var edges = ((object[]?)face.GetEdges() ?? Array.Empty<object>()).ToArray();
        if (edges.Length == 0)
        {
            throw new InvalidOperationException("The selected face does not expose any edges.");
        }

        doc.ClearSelection2(true);
        for (int index = 0; index < edges.Length; index++)
        {
            SelectComObject(doc, edges[index], append: index > 0, mark: 0);
        }

        sketch.SketchUseEdge3(chain: false, innerLoops);
        string? sketchName = (doc.IFeatureByPositionReverse(0) as Feature)?.Name;
        var cutFeature = feature.ExtrudeCut(depth, EndCondition.Blind, flipDirection);

        return new
        {
            faceIndex,
            depth,
            flipDirection,
            innerLoops,
            edgeCount = edges.Length,
            sketchName,
            feature = cutFeature,
        };
    }

    private static void SelectComObject(IModelDoc2 doc, object comObject, bool append, int mark)
    {
        var selectionManager = doc.ISelectionManager
            ?? throw new InvalidOperationException("No selection manager available.");
        var selectData = (SelectData)selectionManager.CreateSelectData();
        selectData.Mark = mark;

        var result = comObject.GetType().InvokeMember(
            "Select4",
            BindingFlags.InvokeMethod,
            binder: null,
            target: comObject,
            args: [append, selectData]);

        bool selected = result switch
        {
            bool boolResult => boolResult,
            int intResult => intResult != 0,
            _ => false,
        };

        if (!selected)
        {
            throw new InvalidOperationException("Failed to select a face edge for SketchUseEdge3.");
        }
    }
}