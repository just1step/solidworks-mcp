using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

public sealed record WorkflowStageEvent(string WorkflowName, string StageName, string Boundary, string? Detail);

public sealed class RecordingWorkflowStageLogger : IWorkflowStageLogger
{
    private readonly List<WorkflowStageEvent> _events = [];

    public IReadOnlyList<WorkflowStageEvent> Events => _events;

    public void LogStage(string workflowName, string stageName, string boundary, string? detail = null)
    {
        _events.Add(new WorkflowStageEvent(workflowName, stageName, boundary, detail));
    }
}