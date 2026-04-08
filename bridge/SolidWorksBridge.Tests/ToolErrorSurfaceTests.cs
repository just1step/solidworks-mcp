using ModelContextProtocol;
using Moq;
using SolidWorksMcpApp;
using SolidWorksMcpApp.Tools;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests;

public class ToolErrorSurfaceTests
{
    [Fact]
    public async Task NewDocument_WithUnknownType_ThrowsMcpExceptionWithReason()
    {
        using var sta = new StaDispatcher();
        var docs = new Mock<IDocumentService>();
        var tool = new DocumentTools(sta, docs.Object);

        var error = await Assert.ThrowsAsync<McpException>(() => tool.NewDocument("SheetMetal"));

        Assert.Contains("Unknown document type 'SheetMetal'", error.Message);
        docs.Verify(d => d.NewDocument(It.IsAny<SwDocType>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ShowStandardView_WithUnknownView_ThrowsMcpExceptionWithReason()
    {
        using var sta = new StaDispatcher();
        var docs = new Mock<IDocumentService>();
        var tool = new DocumentTools(sta, docs.Object);

        var error = await Assert.ThrowsAsync<McpException>(() => tool.ShowStandardView("diagonal"));

        Assert.Contains("Unknown standard view 'diagonal'", error.Message);
        Assert.Contains("isometric", error.Message);
        docs.Verify(d => d.ShowStandardView(It.IsAny<SwStandardView>()), Times.Never);
    }

    [Fact]
    public async Task ListEntities_WithUnknownEntityType_ThrowsMcpExceptionWithReason()
    {
        using var sta = new StaDispatcher();
        var selection = new Mock<ISelectionService>();
        var tool = new SelectionTools(sta, selection.Object);

        var error = await Assert.ThrowsAsync<McpException>(() => tool.ListEntities("Loop"));

        Assert.Contains("Unknown selectable entity type 'Loop'", error.Message);
        Assert.Contains("Face, Edge, or Vertex", error.Message);
        selection.Verify(s => s.ListEntities(It.IsAny<SelectableEntityType?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task AddText_WithUnknownJustification_ThrowsMcpExceptionWithReason()
    {
        using var sta = new StaDispatcher();
        var sketch = new Mock<ISketchService>();
        var tool = new SketchTools(sta, sketch.Object);

        var error = await Assert.ThrowsAsync<McpException>(() => tool.AddText(0, 0, "HELLO", justification: "offcenter"));

        Assert.Contains("Unknown sketch text justification 'offcenter'", error.Message);
        Assert.Contains("fullyJustified", error.Message);
        sketch.Verify(s => s.AddText(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<SketchTextOptions?>()), Times.Never);
    }

    [Fact]
    public async Task ExtrudeCut_WithUnknownEndCondition_ThrowsMcpExceptionWithReason()
    {
        using var sta = new StaDispatcher();
        var feature = new Mock<IFeatureService>();
        var tool = new FeatureTools(sta, feature.Object);

        var error = await Assert.ThrowsAsync<McpException>(() => tool.ExtrudeCut(0.001, endCondition: 5));

        Assert.Contains("endCondition must be 0 (Blind), 1 (ThroughAll), or 6 (MidPlane).", error.Message);
        feature.Verify(f => f.ExtrudeCut(It.IsAny<double>(), It.IsAny<EndCondition>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task AddMateCoincident_WithUnknownAlign_ThrowsMcpExceptionWithReason()
    {
        using var sta = new StaDispatcher();
        var assembly = new Mock<IAssemblyService>();
        var tool = new AssemblyTools(sta, assembly.Object);

        var error = await Assert.ThrowsAsync<McpException>(() => tool.AddMateCoincident(align: 7));

        Assert.Contains("align must be 0 (None), 1 (AntiAligned), or 2 (Closest).", error.Message);
        assembly.Verify(a => a.AddMateCoincident(It.IsAny<MateAlign>()), Times.Never);
    }
}
