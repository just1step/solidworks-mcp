using System.Text.Json;
using Moq;
using SolidWorks.Interop.sldworks;
using SolidWorksMcpApp;
using SolidWorksMcpApp.Tools;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests;

public class WorkflowToolsTests
{
    private sealed class FakeSelectableEdge
    {
        public List<(bool Append, SelectData Data)> Calls { get; } = [];

        public bool Select4(bool append, SelectData data)
        {
            Calls.Add((append, data));
            return true;
        }
    }

    [Fact]
    public async Task CutFaceByProjectedEdges_RunsProvenWorkflowAndReturnsFeature()
    {
        using var sta = new StaDispatcher();

        var connectionManager = new Mock<ISwConnectionManager>();
        var swApp = new Mock<ISldWorksApp>();
        var selection = new Mock<ISelectionService>();
        var sketch = new Mock<ISketchService>();
        var feature = new Mock<IFeatureService>();
        var workflow = new Mock<IWorkflowService>();
        var doc = new Mock<IPartDoc>();
        var model = doc.As<IModelDoc2>();
        var selectionManager = new Mock<SelectionMgr>();
        var selectData = new Mock<SelectData>();
        var face = new Mock<IFace2>();
        var edgeA = new FakeSelectableEdge();
        var edgeB = new FakeSelectableEdge();
        var topSketchFeature = new Mock<Feature>();

        connectionManager.Setup(m => m.EnsureConnected());
        connectionManager.Setup(m => m.SwApp).Returns(swApp.Object);
        swApp.Setup(s => s.IActiveDoc2).Returns(model.Object);

        selection.Setup(s => s.SelectEntity(SelectableEntityType.Face, 3, false, 0, null))
            .Returns(new SelectionResult(true, "selected"));

        model.Setup(d => d.ISelectionManager).Returns(selectionManager.Object);
        selectionManager.Setup(m => m.GetSelectedObject6(1, -1)).Returns(face.Object);
        selectionManager.Setup(m => m.CreateSelectData()).Returns(selectData.Object);
        face.Setup(f => f.GetEdges()).Returns(new object[] { edgeA, edgeB });
        topSketchFeature.Setup(f => f.Name).Returns("Sketch100");
        model.Setup(d => d.IFeatureByPositionReverse(0)).Returns(topSketchFeature.Object);

        feature.Setup(f => f.ExtrudeCut(0.002, EndCondition.Blind, false))
            .Returns(new FeatureInfo("Cut-Extrude100", "ExtrudeCut"));

        var tool = new WorkflowTools(sta, connectionManager.Object, selection.Object, sketch.Object, feature.Object, workflow.Object);

        string json = await tool.CutFaceByProjectedEdges(3, 0.002, false, true);

        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(3, parsed.RootElement.GetProperty("faceIndex").GetInt32());
        Assert.Equal(2, parsed.RootElement.GetProperty("edgeCount").GetInt32());
        Assert.Equal("Sketch100", parsed.RootElement.GetProperty("sketchName").GetString());
        Assert.Equal("Cut-Extrude100", parsed.RootElement.GetProperty("feature").GetProperty("Name").GetString());
        Assert.Equal("ExtrudeCut", parsed.RootElement.GetProperty("feature").GetProperty("Type").GetString());

        selection.Verify(s => s.SelectEntity(SelectableEntityType.Face, 3, false, 0, null), Times.Once);
        sketch.Verify(s => s.InsertSketch(), Times.Once);
        sketch.Verify(s => s.SketchUseEdge3(false, true), Times.Once);
        feature.Verify(f => f.ExtrudeCut(0.002, EndCondition.Blind, false), Times.Once);
        model.Verify(d => d.ClearSelection2(true), Times.Exactly(2));

        Assert.Single(edgeA.Calls);
        Assert.False(edgeA.Calls[0].Append);
        Assert.Same(selectData.Object, edgeA.Calls[0].Data);

        Assert.Single(edgeB.Calls);
        Assert.True(edgeB.Calls[0].Append);
        Assert.Same(selectData.Object, edgeB.Calls[0].Data);
    }

    [Fact]
    public async Task CutFaceByProjectedEdges_WithNonPositiveDepth_Throws()
    {
        using var sta = new StaDispatcher();

        var connectionManager = new Mock<ISwConnectionManager>();
        var selection = new Mock<ISelectionService>();
        var sketch = new Mock<ISketchService>();
        var feature = new Mock<IFeatureService>();
        var workflow = new Mock<IWorkflowService>();
        var swApp = new Mock<ISldWorksApp>();

        var tool = new WorkflowTools(sta, connectionManager.Object, selection.Object, sketch.Object, feature.Object, workflow.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            tool.CutFaceByProjectedEdges(3, 0, false, true));

        connectionManager.Verify(m => m.EnsureConnected(), Times.Never);
        selection.Verify(s => s.SelectEntity(It.IsAny<SelectableEntityType>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
        sketch.Verify(s => s.InsertSketch(), Times.Never);
        feature.Verify(f => f.ExtrudeCut(It.IsAny<double>(), It.IsAny<EndCondition>(), It.IsAny<bool>()), Times.Never);
        _ = swApp;
    }

    [Fact]
    public async Task ReplaceNestedComponentAndVerifyPersistence_DelegatesToWorkflowService()
    {
        using var sta = new StaDispatcher();

        var connectionManager = new Mock<ISwConnectionManager>();
        var selection = new Mock<ISelectionService>();
        var sketch = new Mock<ISketchService>();
        var feature = new Mock<IFeatureService>();
        var workflow = new Mock<IWorkflowService>();

        const string hierarchyPath = "SubAsm-1/Pulley-1";
        const string replacementFilePath = @"C:\NewPulley.sldprt";

        var resolution = new AssemblyTargetResolutionResult(
            null,
            hierarchyPath,
            null,
            true,
            false,
            new ComponentInstanceInfo("Pulley-1", @"C:\OldPulley.sldprt", hierarchyPath, 1),
            "SubAsm-1",
            @"C:\SubAsm.sldasm",
            2,
            new[] { new ComponentInstanceInfo("Pulley-1", @"C:\OldPulley.sldprt", hierarchyPath, 1) });
        var impact = new SharedPartEditImpactResult(
            resolution,
            @"C:\OldPulley.sldprt",
            2,
            new[]
            {
                new ComponentInstanceInfo("Pulley-1", @"C:\OldPulley.sldprt", hierarchyPath, 1),
                new ComponentInstanceInfo("Pulley-2", @"C:\OldPulley.sldprt", "SubAsm-1/Pulley-2", 1),
            },
            false,
            "replace_single_instance_before_edit");
        workflow.Setup(w => w.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, null, hierarchyPath, null, "", 0, true))
            .Returns(new NestedComponentReplacementWorkflowResult(
                resolution,
                impact,
                @"C:\Top.sldasm",
                "SubAsm-1",
                @"C:\SubAsm.sldasm",
                "Pulley-1",
                replacementFilePath,
                true,
                new AssemblyComponentReplacementResult("Pulley-1", replacementFilePath, string.Empty, false, 0, true, true),
                new SwSaveResult(@"C:\SubAsm.sldasm", @"C:\SubAsm.sldasm", "sldasm", false, 0, 0),
                true,
                resolution with { ResolvedInstance = new ComponentInstanceInfo("Pulley-1", replacementFilePath, hierarchyPath, 1) },
                impact with
                {
                    SourceFilePath = replacementFilePath,
                    AffectedInstanceCount = 1,
                    AffectedInstances = new[] { new ComponentInstanceInfo("Pulley-1", replacementFilePath, hierarchyPath, 1) },
                    SafeDirectEdit = true,
                    RecommendedAction = "safe_direct_edit"
                },
                true,
                "completed",
                null));

        var tool = new WorkflowTools(sta, connectionManager.Object, selection.Object, sketch.Object, feature.Object, workflow.Object);

        string json = await tool.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, hierarchyPath: hierarchyPath);

        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("completed", parsed.RootElement.GetProperty("Status").GetString());
        Assert.True(parsed.RootElement.GetProperty("PersistenceVerified").GetBoolean());
        workflow.Verify(w => w.ReplaceNestedComponentAndVerifyPersistence(replacementFilePath, null, hierarchyPath, null, "", 0, true), Times.Once);
    }
}