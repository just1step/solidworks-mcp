using System.Text.Json;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

internal sealed record CrossVersionSmokeExpectation(
    string CompatibilityState,
    string ProductSupportLevel,
    string ConnectionAndIntrospectionSupportLevel,
    string ReadOnlySupportLevel,
    string HighRiskMutationWorkflowsSupportLevel,
    string DirectMutationToolsSupportLevel,
    bool HasCompatibilityAdvisory,
    bool ShouldRunHighRiskWorkflowSmoke,
    bool ShouldRunDirectMutationSmoke);

internal sealed record CrossVersionSmokeAreaReport(
    string AreaId,
    string Status,
    string Summary,
    IReadOnlyList<string> CoveredCapabilities,
    IReadOnlyList<string> Artifacts);

internal sealed record CrossVersionSmokeReport(
    string InteropVersion,
    int? InteropMarketingYear,
    string RuntimeRevisionNumber,
    int? RuntimeMarketingYear,
    string CompatibilityState,
    string ProductSupportLevel,
    string ConnectionAndIntrospectionSupportLevel,
    string ReadOnlySupportLevel,
    string HighRiskMutationWorkflowsSupportLevel,
    string DirectMutationToolsSupportLevel,
    IReadOnlyList<string> Notices,
    IReadOnlyList<CrossVersionSmokeAreaReport> Areas);

internal static class CrossVersionSmokeSuite
{
    public static CrossVersionSmokeExpectation DescribeRuntime(SolidWorksCompatibilityInfo compatibility)
    {
        ArgumentNullException.ThrowIfNull(compatibility);

        string productSupport = GetProductSupportLevel(compatibility);
        string connectionSupport = GetConnectionAndReadOnlySupportLevel(productSupport);
        string readOnlySupport = connectionSupport;
        string highRiskSupport = GetHighRiskWorkflowSupportLevel(productSupport);
        string directMutationSupport = GetDirectMutationSupportLevel(productSupport);

        return new CrossVersionSmokeExpectation(
            compatibility.CompatibilityState,
            productSupport,
            connectionSupport,
            readOnlySupport,
            highRiskSupport,
            directMutationSupport,
            HasCompatibilityAdvisory: !string.Equals(compatibility.CompatibilityState, "certified-baseline", StringComparison.OrdinalIgnoreCase),
            ShouldRunHighRiskWorkflowSmoke: !string.Equals(highRiskSupport, "blocked", StringComparison.OrdinalIgnoreCase),
            ShouldRunDirectMutationSmoke: !string.Equals(directMutationSupport, "blocked", StringComparison.OrdinalIgnoreCase));
    }

    public static void AssertRuntimeClassificationIsConsistent(SolidWorksCompatibilityInfo compatibility)
    {
        ArgumentNullException.ThrowIfNull(compatibility);

        if (compatibility.InteropMarketingYear is null || compatibility.RuntimeVersion.MarketingYear is null)
        {
            Assert.Equal("unknown-version", compatibility.CompatibilityState);
            return;
        }

        int interopMarketingYear = compatibility.InteropMarketingYear.Value;
        int runtimeMarketingYear = compatibility.RuntimeVersion.MarketingYear.Value;

        if (runtimeMarketingYear == interopMarketingYear)
        {
            Assert.Equal("certified-baseline", compatibility.CompatibilityState);
            return;
        }

        if (runtimeMarketingYear == interopMarketingYear + 1)
        {
            Assert.Equal("planned-next-version", compatibility.CompatibilityState);
            return;
        }

        if (runtimeMarketingYear > interopMarketingYear + 1)
        {
            Assert.Equal("unsupported-newer-version", compatibility.CompatibilityState);
            return;
        }

        Assert.Equal("unsupported-older-version", compatibility.CompatibilityState);
    }

    public static CrossVersionSmokeReport CreateReport(
        SolidWorksCompatibilityInfo compatibility,
        CrossVersionSmokeExpectation expectation,
        params CrossVersionSmokeAreaReport[] areas)
    {
        ArgumentNullException.ThrowIfNull(compatibility);
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(areas);

        return new CrossVersionSmokeReport(
            compatibility.InteropVersion,
            compatibility.InteropMarketingYear,
            compatibility.RuntimeVersion.RevisionNumber,
            compatibility.RuntimeVersion.MarketingYear,
            compatibility.CompatibilityState,
            expectation.ProductSupportLevel,
            expectation.ConnectionAndIntrospectionSupportLevel,
            expectation.ReadOnlySupportLevel,
            expectation.HighRiskMutationWorkflowsSupportLevel,
            expectation.DirectMutationToolsSupportLevel,
            compatibility.Notices,
            areas);
    }

    public static void WriteReport(string outputPath, CrossVersionSmokeReport report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(report);

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            outputPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
    }

    private static string GetProductSupportLevel(SolidWorksCompatibilityInfo compatibility)
    {
        if (compatibility.InteropMarketingYear is null || compatibility.RuntimeVersion.MarketingYear is null)
        {
            return "unknown";
        }

        int interopMarketingYear = compatibility.InteropMarketingYear.Value;
        int runtimeMarketingYear = compatibility.RuntimeVersion.MarketingYear.Value;

        if (runtimeMarketingYear == interopMarketingYear)
        {
            return "certified";
        }

        if (runtimeMarketingYear == interopMarketingYear + 1)
        {
            return "targeted";
        }

        if (runtimeMarketingYear == interopMarketingYear + 2)
        {
            return "experimental";
        }

        return "unsupported";
    }

    private static string GetConnectionAndReadOnlySupportLevel(string productSupportLevel)
    {
        return productSupportLevel switch
        {
            "certified" => "certified",
            "targeted" => "targeted",
            "experimental" => "experimental",
            "unsupported" => "unsupported",
            _ => "unknown",
        };
    }

    private static string GetHighRiskWorkflowSupportLevel(string productSupportLevel)
    {
        return productSupportLevel switch
        {
            "certified" => "certified",
            "targeted" => "targeted",
            _ => "blocked",
        };
    }

    private static string GetDirectMutationSupportLevel(string productSupportLevel)
    {
        return productSupportLevel switch
        {
            "certified" => "certified",
            "targeted" => "targeted",
            _ => "blocked",
        };
    }
}
