using SolidWorksBridge.SolidWorks;

namespace SolidWorksMcpApp.Logging;

public sealed class ServerLogWorkflowStageLogger : IWorkflowStageLogger
{
    public void LogStage(string workflowName, string stageName, string boundary, string? detail = null)
    {
        string level = string.Equals(boundary, "failed", StringComparison.OrdinalIgnoreCase)
            ? "WARN"
            : "INFO";

        string message = $"{workflowName} | stage={stageName} | boundary={boundary}";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            message += $" | {detail}";
        }

        ServerLogBuffer.Append(level, "Workflow", message);
    }
}