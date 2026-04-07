using ModelContextProtocol.Server;
using SolidWorks.Interop.sldworks;
using SolidWorksBridge.SolidWorks;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    private sealed record CutFaceByProjectedEdgesResult(
        [property: JsonPropertyName("faceIndex")] int FaceIndex,
        [property: JsonPropertyName("depth")] double Depth,
        [property: JsonPropertyName("flipDirection")] bool FlipDirection,
        [property: JsonPropertyName("innerLoops")] bool InnerLoops,
        [property: JsonPropertyName("edgeCount")] int? EdgeCount,
        [property: JsonPropertyName("sketchName")] string? SketchName,
        [property: JsonPropertyName("feature")] FeatureInfo? Feature,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("failureReason")] string? FailureReason,
        [property: JsonPropertyName("compatibilityAdvisory")] CompatibilityAdvisory? CompatibilityAdvisory);

    [McpServerTool, Description("Collect a structured active-document health report by reading native SolidWorks feature diagnostics, rebuild state, and optional save diagnostics. Use this as a verification gate after risky edits instead of inferring health from screenshots or a single API call.")]
    public async Task<string> DiagnoseActiveDocumentHealth(
        [Description("When true, runs ForceRebuild3 before evaluating the final diagnostics state.")] bool forceRebuild = true,
        [Description("Passed to ForceRebuild3. For assemblies, true rebuilds only the top-level assembly; false includes subassemblies.")] bool topOnly = false,
        [Description("When true, silently saves the active document after diagnostics collection so save warnings and errors are included in the report.")] bool saveDocument = false)
    {
        var payload = new { forceRebuild, topOnly, saveDocument };
        var result = await sta.InvokeLoggedAsync(
            nameof(DiagnoseActiveDocumentHealth),
            payload,
            () => workflow.DiagnoseActiveDocumentHealth(forceRebuild, topOnly, saveDocument));
        return JsonSerializer.Serialize(result);
    }

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
        var compatibilityAdvisory = await sta.InvokeLoggedAsync(
            nameof(CutFaceByProjectedEdges),
            new { faceIndex, depth, flipDirection, innerLoops, phase = "compatibility_advisory" },
            () => CompatibilityPolicy.TryGetAdvisory(connectionManager));
        var compatibilityGate = await sta.InvokeLoggedAsync(
            nameof(CutFaceByProjectedEdges),
            new { faceIndex, depth, flipDirection, innerLoops, phase = "compatibility_gate" },
            () => CompatibilityPolicy.TryCreateHighRiskWorkflowGate(connectionManager, "cut-face-by-projected-edges workflow"));
        if (compatibilityGate != null)
        {
            return JsonSerializer.Serialize(new CutFaceByProjectedEdgesResult(
                faceIndex,
                depth,
                flipDirection,
                innerLoops,
                null,
                null,
                null,
                compatibilityGate.Status,
                compatibilityGate.FailureReason,
                compatibilityGate.CompatibilityAdvisory));
        }

        var info = await sta.InvokeLoggedAsync(
            nameof(CutFaceByProjectedEdges),
            new { faceIndex, depth, flipDirection, innerLoops },
            () => CutFaceByProjectedEdgesCore(faceIndex, depth, flipDirection, innerLoops));
        return JsonSerializer.Serialize(info with { CompatibilityAdvisory = compatibilityAdvisory });
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

    [McpServerTool, Description("Validate two exact component hierarchy paths, run static interference on only that pair, and verify the requested scope was actually checked. Use this instead of a generic assembly-wide interference call when the workflow depends on a trustworthy pair result.")]
    public async Task<string> ReviewTargetedStaticInterference(
        [Description("Exact hierarchy path of the first component instance.")] string firstHierarchyPath,
        [Description("Exact hierarchy path of the second component instance.")] string secondHierarchyPath,
        [Description("When true, coincident or touching faces are treated as interference.")] bool treatCoincidenceAsInterference = false)
    {
        var payload = new { firstHierarchyPath, secondHierarchyPath, treatCoincidenceAsInterference };
        var result = await sta.InvokeLoggedAsync(
            nameof(ReviewTargetedStaticInterference),
            payload,
            () => workflow.ReviewTargetedStaticInterference(
                firstHierarchyPath,
                secondHierarchyPath,
                treatCoincidenceAsInterference));
        return JsonSerializer.Serialize(result);
    }

    private CutFaceByProjectedEdgesResult CutFaceByProjectedEdgesCore(int faceIndex, double depth, bool flipDirection, bool innerLoops)
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

        return new CutFaceByProjectedEdgesResult(
            faceIndex,
            depth,
            flipDirection,
            innerLoops,
            edges.Length,
            sketchName,
            cutFeature,
            "completed",
            null,
            null);
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