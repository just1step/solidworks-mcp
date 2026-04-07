using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

public class CrossVersionSmokeSuiteTests
{
    private static SolidWorksCompatibilityInfo Compatibility(
        string compatibilityState,
        string revisionNumber,
        int? revisionMajor,
        int? marketingYear)
    {
        var runtimeVersion = new SolidWorksRuntimeVersionInfo(
            revisionNumber,
            revisionMajor,
            0,
            0,
            marketingYear,
            new SwBuildNumbers(revisionNumber.Split('.')[0], revisionNumber, string.Empty),
            @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\sldworks.exe");

        return new SolidWorksCompatibilityInfo(
            compatibilityState,
            $"Compatibility state is {compatibilityState}.",
            "32.1.0",
            32,
            2024,
            runtimeVersion,
            new SolidWorksLicenseInfo(0, "swLicenseType_Full", "Full SolidWorks license."),
            Array.Empty<string>());
    }

    [Fact]
    public void DescribeRuntime_OnCertifiedBaseline_AllowsAllSmokeAreas()
    {
        var compatibility = Compatibility("certified-baseline", "32.0.0", 32, 2024);

        var expectation = CrossVersionSmokeSuite.DescribeRuntime(compatibility);

        Assert.Equal("certified", expectation.ProductSupportLevel);
        Assert.Equal("certified", expectation.ConnectionAndIntrospectionSupportLevel);
        Assert.Equal("certified", expectation.ReadOnlySupportLevel);
        Assert.Equal("certified", expectation.HighRiskMutationWorkflowsSupportLevel);
        Assert.Equal("certified", expectation.DirectMutationToolsSupportLevel);
        Assert.False(expectation.HasCompatibilityAdvisory);
        Assert.True(expectation.ShouldRunHighRiskWorkflowSmoke);
        Assert.True(expectation.ShouldRunDirectMutationSmoke);
    }

    [Fact]
    public void DescribeRuntime_OnPlannedNextVersion_RequiresAdvisoryButAllowsMutation()
    {
        var compatibility = Compatibility("planned-next-version", "33.0.0", 33, 2025);

        var expectation = CrossVersionSmokeSuite.DescribeRuntime(compatibility);

        Assert.Equal("targeted", expectation.ProductSupportLevel);
        Assert.Equal("targeted", expectation.ConnectionAndIntrospectionSupportLevel);
        Assert.Equal("targeted", expectation.ReadOnlySupportLevel);
        Assert.Equal("targeted", expectation.HighRiskMutationWorkflowsSupportLevel);
        Assert.Equal("targeted", expectation.DirectMutationToolsSupportLevel);
        Assert.True(expectation.HasCompatibilityAdvisory);
        Assert.True(expectation.ShouldRunHighRiskWorkflowSmoke);
        Assert.True(expectation.ShouldRunDirectMutationSmoke);
    }

    [Fact]
    public void DescribeRuntime_OnExperimentalVersion_BlocksMutationSmokeAreas()
    {
        var compatibility = Compatibility("unsupported-newer-version", "34.0.0", 34, 2026);

        var expectation = CrossVersionSmokeSuite.DescribeRuntime(compatibility);

        Assert.Equal("experimental", expectation.ProductSupportLevel);
        Assert.Equal("experimental", expectation.ConnectionAndIntrospectionSupportLevel);
        Assert.Equal("experimental", expectation.ReadOnlySupportLevel);
        Assert.Equal("blocked", expectation.HighRiskMutationWorkflowsSupportLevel);
        Assert.Equal("blocked", expectation.DirectMutationToolsSupportLevel);
        Assert.True(expectation.HasCompatibilityAdvisory);
        Assert.False(expectation.ShouldRunHighRiskWorkflowSmoke);
        Assert.False(expectation.ShouldRunDirectMutationSmoke);
    }

    [Fact]
    public void DescribeRuntime_OnUnknownVersion_BlocksMutationSmokeAreas()
    {
        var compatibility = Compatibility("unknown-version", "?.?.?", null, null);

        var expectation = CrossVersionSmokeSuite.DescribeRuntime(compatibility);

        Assert.Equal("unknown", expectation.ProductSupportLevel);
        Assert.Equal("unknown", expectation.ConnectionAndIntrospectionSupportLevel);
        Assert.Equal("unknown", expectation.ReadOnlySupportLevel);
        Assert.Equal("blocked", expectation.HighRiskMutationWorkflowsSupportLevel);
        Assert.Equal("blocked", expectation.DirectMutationToolsSupportLevel);
        Assert.True(expectation.HasCompatibilityAdvisory);
        Assert.False(expectation.ShouldRunHighRiskWorkflowSmoke);
        Assert.False(expectation.ShouldRunDirectMutationSmoke);
    }

    [Fact]
    public void AssertRuntimeMatchesSupportMatrix_OnSupportedAndUnsupportedEntries_Passes()
    {
        CrossVersionSmokeSuite.AssertRuntimeClassificationIsConsistent(
            Compatibility("certified-baseline", "32.0.0", 32, 2024));
        CrossVersionSmokeSuite.AssertRuntimeClassificationIsConsistent(
            Compatibility("planned-next-version", "33.0.0", 33, 2025));
        CrossVersionSmokeSuite.AssertRuntimeClassificationIsConsistent(
            Compatibility("unsupported-newer-version", "35.0.0", 35, 2027));
        CrossVersionSmokeSuite.AssertRuntimeClassificationIsConsistent(
            Compatibility("unknown-version", "?.?.?", null, null));
    }
}
