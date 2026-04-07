namespace SolidWorksBridge.SolidWorks;

public record CompatibilityAdvisory(
    string CompatibilityState,
    string AdvisoryLevel,
    string Summary,
    string RuntimeRevisionNumber,
    int? RuntimeMarketingYear,
    string LicenseName,
    IReadOnlyList<string> Notices);

public record CompatibilityGateDecision(
    string Status,
    string FailureReason,
    CompatibilityAdvisory CompatibilityAdvisory);

public static class CompatibilityPolicy
{
    public static bool TryGetCompatibilityInfo(ISwConnectionManager? connectionManager, out SolidWorksCompatibilityInfo compatibility)
    {
        compatibility = default!;
        if (connectionManager == null)
        {
            return false;
        }

        try
        {
            var result = connectionManager.GetCompatibilityInfo();
            if (result == null)
            {
                return false;
            }

            compatibility = result;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static CompatibilityAdvisory? TryGetAdvisory(ISwConnectionManager? connectionManager)
    {
        return TryGetCompatibilityInfo(connectionManager, out var compatibility)
            ? CreateAdvisory(compatibility)
            : null;
    }

    public static CompatibilityAdvisory? CreateAdvisory(SolidWorksCompatibilityInfo compatibility)
    {
        if (string.Equals(compatibility.CompatibilityState, "certified-baseline", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string advisoryLevel = string.Equals(compatibility.CompatibilityState, "planned-next-version", StringComparison.OrdinalIgnoreCase)
            ? "info"
            : "warning";

        return new CompatibilityAdvisory(
            compatibility.CompatibilityState,
            advisoryLevel,
            compatibility.Summary,
            compatibility.RuntimeVersion.RevisionNumber,
            compatibility.RuntimeVersion.MarketingYear,
            compatibility.License.Name,
            compatibility.Notices);
    }

    public static CompatibilityGateDecision? TryCreateHighRiskWorkflowGate(ISwConnectionManager? connectionManager, string operationName)
    {
        return TryGetCompatibilityInfo(connectionManager, out var compatibility)
            ? CreateHighRiskWorkflowGate(compatibility, operationName)
            : null;
    }

    public static CompatibilityGateDecision? CreateHighRiskWorkflowGate(SolidWorksCompatibilityInfo compatibility, string operationName)
    {
        if (!ShouldBlockHighRiskWorkflow(compatibility.CompatibilityState))
        {
            return null;
        }

        var advisory = CreateAdvisory(compatibility)
            ?? new CompatibilityAdvisory(
                compatibility.CompatibilityState,
                "warning",
                compatibility.Summary,
                compatibility.RuntimeVersion.RevisionNumber,
                compatibility.RuntimeVersion.MarketingYear,
                compatibility.License.Name,
                compatibility.Notices);

        return new CompatibilityGateDecision(
            "compatibility_state_blocks_operation",
            $"Blocked {operationName} because the current SolidWorks compatibility state '{compatibility.CompatibilityState}' is not trusted for high-risk mutation workflows. {compatibility.Summary}",
            advisory);
    }

    private static bool ShouldBlockHighRiskWorkflow(string compatibilityState)
    {
        return string.Equals(compatibilityState, "unsupported-older-version", StringComparison.OrdinalIgnoreCase)
            || string.Equals(compatibilityState, "unsupported-newer-version", StringComparison.OrdinalIgnoreCase)
            || string.Equals(compatibilityState, "unknown-version", StringComparison.OrdinalIgnoreCase);
    }
}