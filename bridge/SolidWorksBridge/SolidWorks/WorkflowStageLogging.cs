namespace SolidWorksBridge.SolidWorks;

public interface IWorkflowStageLogger
{
    void LogStage(string workflowName, string stageName, string boundary, string? detail = null);
}

public sealed class NullWorkflowStageLogger : IWorkflowStageLogger
{
    public static NullWorkflowStageLogger Instance { get; } = new();

    private NullWorkflowStageLogger()
    {
    }

    public void LogStage(string workflowName, string stageName, string boundary, string? detail = null)
    {
    }
}