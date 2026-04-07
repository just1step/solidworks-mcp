namespace SolidWorksBridge.SolidWorks;

public record SolidWorksCapabilitySupportInfo(
    string CapabilityId,
    string SupportLevel,
    string Summary);

public record SolidWorksVersionSupportInfo(
    int? MarketingYear,
    string ProductSupportLevel,
    string Summary,
    IReadOnlyList<SolidWorksCapabilitySupportInfo> CapabilitySupport,
    IReadOnlyList<string> Notes);

public record SolidWorksSupportMatrixInfo(
    string InteropVersion,
    int InteropRevisionMajor,
    int? InteropMarketingYear,
    IReadOnlyList<SolidWorksVersionSupportInfo> Versions,
    string OlderVersionPolicy,
    string NewerVersionPolicy,
    string UnknownVersionPolicy);

public static class SolidWorksSupportMatrix
{
    public const string ConnectionAndIntrospectionCapability = "connection-and-introspection";
    public const string ReadOnlyWorkflowsCapability = "read-only-workflows";
    public const string HighRiskMutationWorkflowsCapability = "high-risk-mutation-workflows";
    public const string DirectMutationToolsCapability = "direct-mutation-tools";

    public static SolidWorksSupportMatrixInfo CreateCurrent() =>
        Create(
            SwConnectionManager.CompiledInteropVersion,
            SwConnectionManager.CompiledInteropRevisionMajor,
            SwConnectionManager.CompiledInteropMarketingYear);

    public static SolidWorksSupportMatrixInfo Create(
        string interopVersion,
        int interopRevisionMajor,
        int? interopMarketingYear)
    {
        var versions = interopMarketingYear.HasValue
            ? new[]
            {
                CreateCertifiedEntry(interopMarketingYear.Value),
                CreateTargetedEntry(interopMarketingYear.Value + 1),
                CreateExperimentalEntry(interopMarketingYear.Value + 2),
            }
            : Array.Empty<SolidWorksVersionSupportInfo>();

        return new SolidWorksSupportMatrixInfo(
            interopVersion,
            interopRevisionMajor,
            interopMarketingYear,
            versions,
            "SolidWorks versions older than the compiled interop baseline are unsupported in this repository.",
            "SolidWorks versions newer than the experimental discovery window are unsupported until the matrix is updated with explicit evidence.",
            "If the runtime version cannot be mapped to a SolidWorks marketing year, compatibility remains unknown and high-risk mutation workflows must be blocked.");
    }

    public static SolidWorksVersionSupportInfo ResolveRuntimeSupport(
        string interopVersion,
        int interopRevisionMajor,
        int? interopMarketingYear,
        SolidWorksRuntimeVersionInfo runtimeVersion,
        string compatibilityState)
    {
        if (runtimeVersion.MarketingYear is null)
        {
            return CreateUnknownEntry(runtimeVersion.RevisionNumber, compatibilityState);
        }

        if (!interopMarketingYear.HasValue)
        {
            return CreateUnknownEntry(runtimeVersion.RevisionNumber, compatibilityState);
        }

        int runtimeMarketingYear = runtimeVersion.MarketingYear.Value;
        if (runtimeMarketingYear == interopMarketingYear.Value)
        {
            return CreateCertifiedEntry(runtimeMarketingYear);
        }

        if (runtimeMarketingYear == interopMarketingYear.Value + 1)
        {
            return CreateTargetedEntry(runtimeMarketingYear);
        }

        if (runtimeMarketingYear == interopMarketingYear.Value + 2)
        {
            return CreateExperimentalEntry(runtimeMarketingYear);
        }

        return runtimeMarketingYear < interopMarketingYear.Value
            ? CreateUnsupportedOlderEntry(runtimeMarketingYear)
            : CreateUnsupportedNewerEntry(runtimeMarketingYear);
    }

    private static SolidWorksVersionSupportInfo CreateCertifiedEntry(int marketingYear) =>
        new(
            marketingYear,
            "certified",
            $"SolidWorks {marketingYear} is the validated baseline for the current bridge build.",
            CreateCapabilitySupport(
                connectionAndIntrospection: "certified",
                readOnlyWorkflows: "certified",
                highRiskMutationWorkflows: "certified",
                directMutationTools: "certified"),
            new[]
            {
                "Use this version as the primary support baseline for workflow and tool validation.",
            });

    private static SolidWorksVersionSupportInfo CreateTargetedEntry(int marketingYear) =>
        new(
            marketingYear,
            "targeted",
            $"SolidWorks {marketingYear} is inside the targeted certification window for the current P0 workflow line.",
            CreateCapabilitySupport(
                connectionAndIntrospection: "targeted",
                readOnlyWorkflows: "targeted",
                highRiskMutationWorkflows: "targeted",
                directMutationTools: "targeted"),
            new[]
            {
                "Regressions on this version should be treated as active compatibility bugs until certification is complete.",
            });

    private static SolidWorksVersionSupportInfo CreateExperimentalEntry(int marketingYear) =>
        new(
            marketingYear,
            "experimental",
            $"SolidWorks {marketingYear} is in the early compatibility discovery window for this bridge build.",
            CreateCapabilitySupport(
                connectionAndIntrospection: "experimental",
                readOnlyWorkflows: "experimental",
                highRiskMutationWorkflows: "blocked",
                directMutationTools: "blocked"),
            new[]
            {
                "Use this version for blocker discovery and smoke validation, not for trusted high-risk mutation workflows.",
            });

    private static SolidWorksVersionSupportInfo CreateUnsupportedOlderEntry(int marketingYear) =>
        new(
            marketingYear,
            "unsupported",
            $"SolidWorks {marketingYear} is older than the compiled interop baseline and is not a supported target for this bridge build.",
            CreateCapabilitySupport(
                connectionAndIntrospection: "unsupported",
                readOnlyWorkflows: "unsupported",
                highRiskMutationWorkflows: "blocked",
                directMutationTools: "blocked"),
            new[]
            {
                "Use the validated baseline or targeted next version instead of relying on back-compat assumptions.",
            });

    private static SolidWorksVersionSupportInfo CreateUnsupportedNewerEntry(int marketingYear) =>
        new(
            marketingYear,
            "unsupported",
            $"SolidWorks {marketingYear} is newer than the current experimental discovery window and has no declared support status yet.",
            CreateCapabilitySupport(
                connectionAndIntrospection: "unsupported",
                readOnlyWorkflows: "unsupported",
                highRiskMutationWorkflows: "blocked",
                directMutationTools: "blocked"),
            new[]
            {
                "Update the support matrix with new evidence before treating this version as targeted or experimental.",
            });

    private static SolidWorksVersionSupportInfo CreateUnknownEntry(string revisionNumber, string compatibilityState) =>
        new(
            MarketingYear: null,
            ProductSupportLevel: "unknown",
            Summary: $"The runtime revision '{revisionNumber}' could not be mapped to a supported SolidWorks marketing year, so compatibility remains {compatibilityState}.",
            CapabilitySupport: CreateCapabilitySupport(
                connectionAndIntrospection: "unknown",
                readOnlyWorkflows: "unknown",
                highRiskMutationWorkflows: "blocked",
                directMutationTools: "blocked"),
            Notes: new[]
            {
                "Block high-risk mutation workflows until the runtime version is identified and placed in the support matrix.",
            });

    private static IReadOnlyList<SolidWorksCapabilitySupportInfo> CreateCapabilitySupport(
        string connectionAndIntrospection,
        string readOnlyWorkflows,
        string highRiskMutationWorkflows,
        string directMutationTools)
    {
        return new[]
        {
            new SolidWorksCapabilitySupportInfo(
                ConnectionAndIntrospectionCapability,
                connectionAndIntrospection,
                DescribeCapabilitySupport(ConnectionAndIntrospectionCapability, connectionAndIntrospection)),
            new SolidWorksCapabilitySupportInfo(
                ReadOnlyWorkflowsCapability,
                readOnlyWorkflows,
                DescribeCapabilitySupport(ReadOnlyWorkflowsCapability, readOnlyWorkflows)),
            new SolidWorksCapabilitySupportInfo(
                HighRiskMutationWorkflowsCapability,
                highRiskMutationWorkflows,
                DescribeCapabilitySupport(HighRiskMutationWorkflowsCapability, highRiskMutationWorkflows)),
            new SolidWorksCapabilitySupportInfo(
                DirectMutationToolsCapability,
                directMutationTools,
                DescribeCapabilitySupport(DirectMutationToolsCapability, directMutationTools)),
        };
    }

    private static string DescribeCapabilitySupport(string capabilityId, string supportLevel)
    {
        return (capabilityId, supportLevel) switch
        {
            (HighRiskMutationWorkflowsCapability, "blocked") => "High-risk mutation workflows must not claim trusted execution on this support level.",
            (DirectMutationToolsCapability, "blocked") => "Direct mutation tools should not run without an explicit support-level upgrade for this version.",
            (_, "certified") => "Validated for the current bridge baseline.",
            (_, "targeted") => "In active certification scope for the current compatibility line.",
            (_, "experimental") => "Allowed only for evidence-gathering and early compatibility discovery.",
            (_, "unsupported") => "Outside the declared support window for this bridge build.",
            (_, "unknown") => "Runtime support cannot be classified from the available version metadata.",
            _ => $"Support level '{supportLevel}' has no documented summary yet.",
        };
    }
}