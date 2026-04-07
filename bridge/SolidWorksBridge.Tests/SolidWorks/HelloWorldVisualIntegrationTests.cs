using SolidWorksBridge.SolidWorks;
using SolidWorksMcpApp.Logging;

namespace SolidWorksBridge.Tests.SolidWorks;

[Collection("SolidWorks Integration")]
public class HelloWorldVisualIntegrationTests : IDisposable
{
    private const double UnitSize = 0.006;
    private const double PartDepth = 0.005;
    private const double LetterGap = UnitSize * 1.5;
    private const double WordGap = UnitSize * 3.0;
    private const double DimensionTolerance = 1e-6;
    private const double FaceTolerance = 1e-9;
    private static readonly string ArtifactRunId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly IReadOnlyDictionary<char, LetterDefinition> LetterDefinitions = new Dictionary<char, LetterDefinition>
    {
        ['H'] = new(
            WidthUnits: 7,
            Placements:
            [
                new LetterPlacement("V7", 0, 0, 1, 7),
                new LetterPlacement("V7", 6, 0, 7, 7),
                new LetterPlacement("H5", 1, 3, 6, 4),
            ]),
        ['E'] = new(
            WidthUnits: 6,
            Placements:
            [
                new LetterPlacement("V7", 0, 0, 1, 7),
                new LetterPlacement("H5", 1, 6, 6, 7),
                new LetterPlacement("H3", 1, 3, 4, 4),
                new LetterPlacement("H5", 1, 0, 6, 1),
            ]),
        ['L'] = new(
            WidthUnits: 6,
            Placements:
            [
                new LetterPlacement("V7", 0, 0, 1, 7),
                new LetterPlacement("H5", 1, 0, 6, 1),
            ]),
        ['O'] = new(
            WidthUnits: 7,
            Placements:
            [
                new LetterPlacement("C1", 0, 6, 1, 7),
                new LetterPlacement("H5", 1, 6, 6, 7),
                new LetterPlacement("C1", 6, 6, 7, 7),
                new LetterPlacement("V5", 0, 1, 1, 6),
                new LetterPlacement("V5", 6, 1, 7, 6),
                new LetterPlacement("C1", 0, 0, 1, 1),
                new LetterPlacement("H5", 1, 0, 6, 1),
                new LetterPlacement("C1", 6, 0, 7, 1),
            ]),
        ['W'] = new(
            WidthUnits: 7,
            Placements:
            [
                new LetterPlacement("V7", 0, 0, 1, 7),
                new LetterPlacement("V7", 2, 0, 3, 7),
                new LetterPlacement("V7", 4, 0, 5, 7),
                new LetterPlacement("V7", 6, 0, 7, 7),
                new LetterPlacement("C1", 1, 0, 2, 1),
                new LetterPlacement("C1", 3, 0, 4, 1),
                new LetterPlacement("C1", 5, 0, 6, 1),
            ]),
        ['R'] = new(
            WidthUnits: 6,
            Placements:
            [
                new LetterPlacement("V7", 0, 0, 1, 7),
                new LetterPlacement("H3", 1, 6, 4, 7),
                new LetterPlacement("V3", 4, 4, 5, 7),
                new LetterPlacement("H3", 1, 3, 4, 4),
                new LetterPlacement("C1", 3, 2, 4, 3),
                new LetterPlacement("C1", 4, 1, 5, 2),
                new LetterPlacement("C1", 5, 0, 6, 1),
            ]),
        ['D'] = new(
            WidthUnits: 6,
            Placements:
            [
                new LetterPlacement("V7", 0, 0, 1, 7),
                new LetterPlacement("V7", 5, 0, 6, 7),
                new LetterPlacement("H4", 1, 6, 5, 7),
                new LetterPlacement("H4", 1, 0, 5, 1),
            ]),
    };

    private static readonly PrimitivePartSpec[] PrimitiveParts =
    [
        new("V7", "bar-v7.sldprt", PrimitivePartShape.BarWithTopHole, UnitSize, UnitSize * 7, PartDepth),
        new("V7R", "bar-v7-replacement.sldprt", PrimitivePartShape.BarWithTopHoleAndEndPocket, UnitSize, UnitSize * 7, PartDepth),
        new("H5", "bar-h5.sldprt", PrimitivePartShape.BarWithFrontHole, UnitSize * 5, UnitSize, PartDepth),
        new("H4", "bar-h4.sldprt", PrimitivePartShape.BarWithFrontHole, UnitSize * 4, UnitSize, PartDepth),
        new("H3", "bar-h3.sldprt", PrimitivePartShape.BarPlain, UnitSize * 3, UnitSize, PartDepth),
        new("V5", "bar-v5.sldprt", PrimitivePartShape.BarWithTopHole, UnitSize, UnitSize * 5, PartDepth),
        new("V3", "bar-v3.sldprt", PrimitivePartShape.BarWithTopHole, UnitSize, UnitSize * 3, PartDepth),
        new("C1", "cube-c1.sldprt", PrimitivePartShape.FilletedCube, UnitSize, UnitSize, PartDepth),
    ];

    private static readonly (SwStandardView View, string Suffix)[] VerificationViews =
    [
        (SwStandardView.Front, "front"),
        (SwStandardView.Top, "top"),
        (SwStandardView.Right, "right"),
        (SwStandardView.Isometric, "iso"),
    ];

    private readonly SolidWorksIntegrationTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string? GetParentHierarchyPath(string hierarchyPath)
    {
        int separatorIndex = hierarchyPath.LastIndexOf('/');
        return separatorIndex < 0 ? null : hierarchyPath[..separatorIndex];
    }

    private static string GetArtifactDirectory(string scenario)
    {
        string scenarioDirectory = Path.Combine(RepositoryRoot, "artifacts", "integration-visuals", scenario);
        Directory.CreateDirectory(scenarioDirectory);

        string runDirectory = Path.Combine(scenarioDirectory, ArtifactRunId);
        Directory.CreateDirectory(runDirectory);
        return runDirectory;
    }

    private static string GetArtifactPath(string outputDirectory, string fileName)
    {
        return Path.Combine(outputDirectory, $"{ArtifactRunId}-{fileName}");
    }

    private static string ExpectedAdvisoryLevel(string compatibilityState)
    {
        return string.Equals(compatibilityState, "planned-next-version", StringComparison.OrdinalIgnoreCase)
            ? "info"
            : "warning";
    }

    private (SolidWorksCompatibilityInfo Compatibility, CrossVersionSmokeExpectation Expectation) GetSmokeContext()
    {
        var compatibility = _ctx.ConnectionManager.GetCompatibilityInfo();
        CrossVersionSmokeSuite.AssertRuntimeClassificationIsConsistent(compatibility);
        var expectation = CrossVersionSmokeSuite.DescribeRuntime(compatibility);
        return (compatibility, expectation);
    }

    private string WriteSmokeReport(
        string outputDirectory,
        string fileName,
        SolidWorksCompatibilityInfo compatibility,
        CrossVersionSmokeExpectation expectation,
        params CrossVersionSmokeAreaReport[] areas)
    {
        string reportPath = GetArtifactPath(outputDirectory, fileName);
        var report = CrossVersionSmokeSuite.CreateReport(compatibility, expectation, areas);
        CrossVersionSmokeSuite.WriteReport(reportPath, report);
        Assert.True(File.Exists(reportPath), $"Expected smoke report to exist: {reportPath}");
        return reportPath;
    }

    private void AssertCompatibilityAdvisoryMatchesRuntime(CompatibilityAdvisory? advisory)
    {
        var compatibility = _ctx.ConnectionManager.GetCompatibilityInfo();
        if (string.Equals(compatibility.CompatibilityState, "certified-baseline", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Null(advisory);
            return;
        }

        Assert.NotNull(advisory);
        Assert.Equal(compatibility.CompatibilityState, advisory!.CompatibilityState);
        Assert.Equal(ExpectedAdvisoryLevel(compatibility.CompatibilityState), advisory.AdvisoryLevel);
        Assert.Equal(compatibility.Summary, advisory.Summary);
        Assert.Equal(compatibility.RuntimeVersion.RevisionNumber, advisory.RuntimeRevisionNumber);
        Assert.Equal(compatibility.RuntimeVersion.MarketingYear, advisory.RuntimeMarketingYear);
        Assert.Equal(compatibility.License.Name, advisory.LicenseName);
        Assert.Equal(compatibility.Notices, advisory.Notices);
    }

    private static void AssertRuntimeSupportMatchesMatrix(
        SolidWorksCompatibilityInfo compatibility,
        SolidWorksSupportMatrixInfo supportMatrix)
    {
        Assert.NotNull(compatibility.RuntimeSupport);

        var runtimeSupport = compatibility.RuntimeSupport!;
        if (compatibility.RuntimeVersion.MarketingYear is int runtimeMarketingYear)
        {
            var explicitEntry = supportMatrix.Versions.SingleOrDefault(entry => entry.MarketingYear == runtimeMarketingYear);
            if (explicitEntry is not null)
            {
                Assert.Equal(explicitEntry.ProductSupportLevel, runtimeSupport.ProductSupportLevel);
                AssertCapabilitySupportMatches(runtimeSupport, explicitEntry, SolidWorksSupportMatrix.ConnectionAndIntrospectionCapability);
                AssertCapabilitySupportMatches(runtimeSupport, explicitEntry, SolidWorksSupportMatrix.ReadOnlyWorkflowsCapability);
                AssertCapabilitySupportMatches(runtimeSupport, explicitEntry, SolidWorksSupportMatrix.HighRiskMutationWorkflowsCapability);
                AssertCapabilitySupportMatches(runtimeSupport, explicitEntry, SolidWorksSupportMatrix.DirectMutationToolsCapability);
                return;
            }

            Assert.Equal("unsupported", runtimeSupport.ProductSupportLevel);
            return;
        }

        Assert.Equal("unknown", runtimeSupport.ProductSupportLevel);
    }

    private static void AssertCapabilitySupportMatches(
        SolidWorksVersionSupportInfo runtimeSupport,
        SolidWorksVersionSupportInfo expectedSupport,
        string capabilityId)
    {
        string actual = runtimeSupport.CapabilitySupport
            .Single(entry => string.Equals(entry.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase))
            .SupportLevel;
        string expected = expectedSupport.CapabilitySupport
            .Single(entry => string.Equals(entry.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase))
            .SupportLevel;

        Assert.Equal(expected, actual);
    }

    private enum PrimitivePartShape
    {
        BarPlain,
        BarWithTopHole,
        BarWithTopHoleAndEndPocket,
        BarWithFrontHole,
        BarThroughAll,
        FilletedCube,
        Ring,
    }

    private sealed record PrimitivePartSpec(string Key, string FileName, PrimitivePartShape Shape, double Width, double Height, double Depth);
    private sealed record LetterPlacement(string PartKey, int X1, int Y1, int X2, int Y2);
    private sealed record LetterDefinition(double WidthUnits, IReadOnlyList<LetterPlacement> Placements, bool UseAssemblyMates = false);
    private sealed record InsertedLetterComponent(string PartKey, LetterPlacement Placement, ComponentInfo Component);
    private sealed record LetterAssemblyArtifact(string AssemblyPath, double WidthMeters);
    private sealed record HelloWorldAssemblyScenario(
        string ParentAssemblyPath,
        string OriginalVerticalPartPath,
        string ReplacementVerticalPartPath,
        string UniqueNestedPartPath,
        string TargetHierarchyPath,
        string TopLevelComponentName,
        int OriginalVerticalReuseCount);

    private enum Axis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    private void SelectFrontPlane()
    {
        var frontPlane = _ctx.Selection.ListReferencePlanes()
            .OrderBy(plane => plane.Index)
            .First();
        var selected = _ctx.Selection.SelectByName(frontPlane.SelectionName, frontPlane.SelectionType);
        Assert.True(selected.Success, selected.Message);
    }

    private void SelectTopPlane()
    {
        var planes = _ctx.Selection.ListReferencePlanes()
            .OrderBy(plane => plane.Index)
            .ToList();
        Assert.True(planes.Count >= 2, "Expected at least two default reference planes.");
        var selected = _ctx.Selection.SelectByName(planes[1].SelectionName, planes[1].SelectionType);
        Assert.True(selected.Success, selected.Message);
    }

    private SelectableEntityInfo GetExtremePartFace(Axis axis, bool selectMax)
    {
        int axisIndex = (int)axis;
        var faces = _ctx.Selection.ListEntities(SelectableEntityType.Face)
            .Where(face => face.Box is { Length: >= 6 } box && Math.Abs(box[axisIndex] - box[axisIndex + 3]) <= FaceTolerance)
            .OrderBy(face => face.Box![axisIndex])
            .ToList();

        Assert.NotEmpty(faces);
        return selectMax ? faces[^1] : faces[0];
    }

    private void SelectPartFace(Axis axis, bool selectMax)
    {
        var face = GetExtremePartFace(axis, selectMax);
        var selected = _ctx.Selection.SelectEntity(SelectableEntityType.Face, face.Index);
        Assert.True(selected.Success, selected.Message);
        Assert.Equal(SelectableEntityType.Face, selected.EntityType);
        Assert.Equal(face.Index, selected.EntityIndex);
        Assert.Equal(face.Box, selected.Box);
    }

    private SelectableEntityInfo GetExtremeComponentFace(string componentName, Axis axis, bool selectMax)
    {
        int axisIndex = (int)axis;
        var faces = _ctx.Selection.ListEntities(SelectableEntityType.Face, componentName)
            .Where(face => face.Box is { Length: >= 6 } box && Math.Abs(box[axisIndex] - box[axisIndex + 3]) <= FaceTolerance)
            .OrderBy(face => face.Box![axisIndex])
            .ToList();

        Assert.NotEmpty(faces);
        return selectMax ? faces[^1] : faces[0];
    }

    private void SelectComponentFace(string componentName, Axis axis, bool selectMax, bool append)
    {
        var face = GetExtremeComponentFace(componentName, axis, selectMax);
        var selection = _ctx.Selection.SelectEntity(
            SelectableEntityType.Face,
            face.Index,
            append: append,
            componentName: componentName);
        Assert.True(selection.Success, selection.Message);
        Assert.Equal(SelectableEntityType.Face, selection.EntityType);
        Assert.Equal(face.Index, selection.EntityIndex);
        Assert.Equal(componentName, selection.ComponentName);
        Assert.Equal(face.Box, selection.Box);
    }

    private void AddCoincidentMate(string firstComponentName, Axis firstAxis, bool firstSelectMax, string secondComponentName, Axis secondAxis, bool secondSelectMax)
    {
        SelectComponentFace(firstComponentName, firstAxis, firstSelectMax, append: false);
        SelectComponentFace(secondComponentName, secondAxis, secondSelectMax, append: true);
        _ = _ctx.Assembly.AddMateCoincident();
    }

    private void AddDistanceMate(string firstComponentName, Axis firstAxis, bool firstSelectMax, string secondComponentName, Axis secondAxis, bool secondSelectMax, double distance)
    {
        SelectComponentFace(firstComponentName, firstAxis, firstSelectMax, append: false);
        SelectComponentFace(secondComponentName, secondAxis, secondSelectMax, append: true);
        _ = _ctx.Assembly.AddMateDistance(distance);
    }

    private double MeasureFaceDistance(string firstComponentName, Axis firstAxis, bool firstSelectMax, string secondComponentName, Axis secondAxis, bool secondSelectMax)
    {
        var firstFace = GetExtremeComponentFace(firstComponentName, firstAxis, firstSelectMax);
        var secondFace = GetExtremeComponentFace(secondComponentName, secondAxis, secondSelectMax);
        var measurement = _ctx.Selection.MeasureEntities(
            SelectableEntityType.Face,
            firstFace.Index,
            SelectableEntityType.Face,
            secondFace.Index,
            firstComponentName,
            secondComponentName);

        Assert.NotNull(measurement.Distance);
        return measurement.Distance!.Value;
    }

    private string CreateSketchCapabilitySheet(string outputDirectory)
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        var missing = _ctx.Selection.SelectByName("__missing__", "PLANE");
        Assert.False(missing.Success);

        SelectTopPlane();
        _ctx.Selection.ClearSelection();
        SelectFrontPlane();
        _ctx.Sketch.InsertSketch();

        Assert.Equal("Point", _ctx.Sketch.AddPoint(-0.04, 0.03).Type);
        Assert.Equal("Line", _ctx.Sketch.AddLine(-0.05, -0.02, -0.01, -0.02).Type);
        Assert.Equal("Arc", _ctx.Sketch.AddArc(-0.025, 0.005, -0.01, 0.005, -0.025, 0.02, 1).Type);
        Assert.Equal("Ellipse", _ctx.Sketch.AddEllipse(0.02, 0.015, 0.04, 0.015, 0.02, 0.005).Type);
        Assert.Equal("Polygon", _ctx.Sketch.AddPolygon(0.055, 0.015, 0.07, 0.015, 6, true).Type);
        Assert.Equal("Text", _ctx.Sketch.AddText(-0.005, 0.028, "HELLO WORLD").Type);
        Assert.Equal("Circle", _ctx.Sketch.AddCircle(0.02, -0.02, 0.008).Type);
        Assert.Equal("Rectangle", _ctx.Sketch.AddRectangle(0.045, -0.03, 0.075, -0.005).Type);

        _ctx.Sketch.FinishSketch();

        string outputPath = GetArtifactPath(outputDirectory, "hello-world-sketch-sheet.sldprt");
        var save = _ctx.Documents.SaveDocumentAs(outputPath, sourcePath: null, saveAsCopy: false);
        Assert.Equal(Path.GetFullPath(outputPath), save.OutputPath, StringComparer.OrdinalIgnoreCase);
        return outputPath;
    }

    private string CreatePrimitivePart(string outputPath, PrimitivePartSpec spec)
    {
        _ctx.Documents.NewDocument(SwDocType.Part);
        SelectFrontPlane();
        _ctx.Sketch.InsertSketch();
        _ctx.Sketch.AddRectangle(-spec.Width / 2.0, -spec.Height / 2.0, spec.Width / 2.0, spec.Height / 2.0);

        FeatureInfo baseFeature = spec.Shape == PrimitivePartShape.BarThroughAll
            ? _ctx.Feature.Extrude(0.001, EndCondition.ThroughAll)
            : _ctx.Feature.Extrude(spec.Depth);
        Assert.Equal("Extrude", baseFeature.Type);

        switch (spec.Shape)
        {
            case PrimitivePartShape.BarWithTopHole:
                SelectPartFace(Axis.Z, true);
                _ctx.Sketch.InsertSketch();
                _ctx.Sketch.AddCircle(0, spec.Height / 2.0 - UnitSize, UnitSize * 0.25);
                _ctx.Sketch.FinishSketch();
                Assert.Equal("ExtrudeCut", _ctx.Feature.ExtrudeCut(spec.Depth * 3, EndCondition.ThroughAll).Type);
                break;

            case PrimitivePartShape.BarWithTopHoleAndEndPocket:
                SelectPartFace(Axis.Z, true);
                _ctx.Sketch.InsertSketch();
                _ctx.Sketch.AddCircle(0, spec.Height / 2.0 - UnitSize, UnitSize * 0.25);
                _ctx.Sketch.FinishSketch();
                Assert.Equal("ExtrudeCut", _ctx.Feature.ExtrudeCut(spec.Depth * 3, EndCondition.ThroughAll).Type);

                // Preserve the faces used by the H subassembly mates and place the distinguishing change on the end face.
                SelectPartFace(Axis.Y, true);
                _ctx.Sketch.InsertSketch();
                _ctx.Sketch.AddCircle(0, 0, Math.Min(spec.Width, spec.Depth) * 0.18);
                _ctx.Sketch.FinishSketch();
                Assert.Equal("ExtrudeCut", _ctx.Feature.ExtrudeCut(UnitSize * 1.5).Type);
                break;

            case PrimitivePartShape.BarWithFrontHole:
                SelectPartFace(Axis.Z, true);
                _ctx.Sketch.InsertSketch();
                _ctx.Sketch.AddCircle(0, 0, UnitSize * 0.25);
                Assert.Equal("ExtrudeCut", _ctx.Feature.ExtrudeCut(spec.Depth * 3, EndCondition.ThroughAll).Type);
                break;

            case PrimitivePartShape.Ring:
                SelectPartFace(Axis.Z, true);
                _ctx.Sketch.InsertSketch();
                _ctx.Sketch.AddCircle(0, 0, spec.Width / 4.0);
                Assert.Equal("ExtrudeCut", _ctx.Feature.ExtrudeCut(spec.Depth * 3, EndCondition.ThroughAll).Type);
                break;
        }

        var firstEdge = _ctx.Selection.ListEntities(SelectableEntityType.Edge).First();
        var edgeSelection = _ctx.Selection.SelectEntity(SelectableEntityType.Edge, firstEdge.Index);
        Assert.True(edgeSelection.Success, edgeSelection.Message);
        Assert.Equal(SelectableEntityType.Edge, edgeSelection.EntityType);
        Assert.Equal(firstEdge.Index, edgeSelection.EntityIndex);
        Assert.Equal("Fillet", _ctx.Feature.Fillet(UnitSize * 0.12).Type);

        string path = Path.Combine(Path.GetDirectoryName(outputPath)!, Path.GetFileName(outputPath));
        var save = _ctx.Documents.SaveDocumentAs(path, sourcePath: null, saveAsCopy: false);
        Assert.True(File.Exists(path), $"Expected reusable part to exist: {path}");
        Assert.Equal(Path.GetFullPath(path), save.OutputPath, StringComparer.OrdinalIgnoreCase);
        return path;
    }

    private Dictionary<string, string> CreatePrimitivePartLibrary(string outputDirectory)
    {
        var library = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in PrimitiveParts)
        {
            library[spec.Key] = CreatePrimitivePart(GetArtifactPath(outputDirectory, spec.FileName), spec);
        }

        return library;
    }

    private static (double CenterX, double CenterY) GetPlacementCenter(LetterPlacement placement)
    {
        double centerX = ((placement.X1 + placement.X2) / 2.0) * UnitSize;
        double centerY = ((placement.Y1 + placement.Y2) / 2.0) * UnitSize;
        return (centerX, centerY);
    }

    private void ApplyHMates(IReadOnlyList<InsertedLetterComponent> insertedComponents)
    {
        var verticals = insertedComponents
            .Where(component => string.Equals(component.PartKey, "V7", StringComparison.OrdinalIgnoreCase))
            .OrderBy(component => GetPlacementCenter(component.Placement).CenterX)
            .ToList();
        Assert.Equal(2, verticals.Count);

        var bridge = insertedComponents.Single(component => string.Equals(component.PartKey, "H5", StringComparison.OrdinalIgnoreCase));
        var leftVertical = verticals[0];
        var rightVertical = verticals[1];

        AddCoincidentMate(leftVertical.Component.Name, Axis.X, true, bridge.Component.Name, Axis.X, false);
        AddCoincidentMate(bridge.Component.Name, Axis.X, true, rightVertical.Component.Name, Axis.X, false);
        AddCoincidentMate(leftVertical.Component.Name, Axis.Z, false, bridge.Component.Name, Axis.Z, false);
        AddCoincidentMate(rightVertical.Component.Name, Axis.Z, false, bridge.Component.Name, Axis.Z, false);

        double measuredWidth = MeasureFaceDistance(leftVertical.Component.Name, Axis.X, false, rightVertical.Component.Name, Axis.X, true);
        Assert.InRange(measuredWidth, (7 * UnitSize) - DimensionTolerance, (7 * UnitSize) + DimensionTolerance);

        double measuredBridgeHeight = MeasureFaceDistance(leftVertical.Component.Name, Axis.Y, false, bridge.Component.Name, Axis.Y, false);
        Assert.InRange(measuredBridgeHeight, (3 * UnitSize) - DimensionTolerance, (3 * UnitSize) + DimensionTolerance);
    }

    private LetterAssemblyArtifact CreateLetterAssembly(char letter, IReadOnlyDictionary<string, string> primitiveParts, string outputDirectory)
    {
        if (!LetterDefinitions.TryGetValue(letter, out var definition))
        {
            throw new InvalidOperationException($"Unsupported reusable HELLO WORLD letter '{letter}'.");
        }

        _ctx.Documents.NewDocument(SwDocType.Assembly);
        var insertedComponents = new List<InsertedLetterComponent>(definition.Placements.Count);
        foreach (var placement in definition.Placements)
        {
            string partPath = primitiveParts[placement.PartKey];
            var (centerX, centerY) = GetPlacementCenter(placement);
            var inserted = _ctx.Assembly.InsertComponent(partPath, centerX, centerY, 0);
            insertedComponents.Add(new InsertedLetterComponent(placement.PartKey, placement, inserted));
        }

        if (definition.UseAssemblyMates)
        {
            ApplyHMates(insertedComponents);
        }

        string assemblyPath = GetArtifactPath(outputDirectory, $"letter-{char.ToLowerInvariant(letter)}.sldasm");
        var save = _ctx.Documents.SaveDocumentAs(assemblyPath, sourcePath: null, saveAsCopy: false);
        Assert.Equal(Path.GetFullPath(assemblyPath), save.OutputPath, StringComparer.OrdinalIgnoreCase);
        return new LetterAssemblyArtifact(assemblyPath, definition.WidthUnits * UnitSize);
    }

    private void ValidateScratchAssemblyCapabilities(string reusableVerticalPartPath)
    {
        _ctx.Documents.NewDocument(SwDocType.Assembly);
        Assert.Empty(_ctx.Assembly.ListComponents());

        var first = _ctx.Assembly.InsertComponent(reusableVerticalPartPath, 0, 0, 0);
        var second = _ctx.Assembly.InsertComponent(reusableVerticalPartPath, 0, 0, 0);
        var components = _ctx.Assembly.ListComponents();
        var recursiveComponents = _ctx.Assembly.ListComponentsRecursive();
        Assert.Equal(2, components.Count);
        Assert.Equal(2, recursiveComponents.Count);
        Assert.NotEqual(first.Name, second.Name);

        int initialLogCount = ServerLogBuffer.GetSnapshot().Count;
        var workflowLogger = new ServerLogWorkflowStageLogger();
        var workflow = new WorkflowService(_ctx.Documents, _ctx.Assembly, null, _ctx.ConnectionManager, workflowLogger);
        var interferingReview = workflow.ReviewTargetedStaticInterference(first.Name, second.Name);
        Assert.Equal("completed", interferingReview.Status);
        AssertCompatibilityAdvisoryMatchesRuntime(interferingReview.CompatibilityAdvisory);
        Assert.True(interferingReview.ScopeValidated);
        Assert.True(interferingReview.ScopeEvaluatedAsRequested);
        Assert.True(interferingReview.HasInterference);
        Assert.NotNull(interferingReview.InterferenceCheck);
        Assert.Equal(2, interferingReview.InterferenceCheck!.CheckedComponentCount);
        Assert.Contains(interferingReview.InterferenceCheck.InterferingComponents, component =>
            string.Equals(component.HierarchyPath, first.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(interferingReview.InterferenceCheck.InterferingComponents, component =>
            string.Equals(component.HierarchyPath, second.Name, StringComparison.OrdinalIgnoreCase));

        var staleReview = workflow.ReviewTargetedStaticInterference(first.Name, "Missing/Component-1");
        Assert.Equal("second_target_not_resolved", staleReview.Status);
        AssertCompatibilityAdvisoryMatchesRuntime(staleReview.CompatibilityAdvisory);
        Assert.False(staleReview.ScopeValidated);
        Assert.Null(staleReview.InterferenceCheck);

        AddCoincidentMate(first.Name, Axis.Z, false, second.Name, Axis.Z, false);
        var interference = _ctx.Assembly.CheckInterference([first.Name, second.Name]);
        Assert.True(interference.HasInterference);
        Assert.Equal(2, interference.CheckedComponentCount);

        AddDistanceMate(first.Name, Axis.X, true, second.Name, Axis.X, false, UnitSize);
        var separatedReview = workflow.ReviewTargetedStaticInterference(first.Name, second.Name);
        Assert.Equal("completed", separatedReview.Status);
        AssertCompatibilityAdvisoryMatchesRuntime(separatedReview.CompatibilityAdvisory);
        Assert.True(separatedReview.ScopeEvaluatedAsRequested);
        Assert.False(separatedReview.HasInterference);
        Assert.NotNull(separatedReview.InterferenceCheck);
        Assert.Equal(2, separatedReview.InterferenceCheck!.CheckedComponentCount);
        var separated = _ctx.Assembly.CheckInterference([first.Name, second.Name]);
        Assert.False(separated.HasInterference, "Distance mate should separate the scratch components and remove interference.");

        var workflowEntries = ServerLogBuffer.GetSnapshot().Skip(initialLogCount).ToArray();
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.ReviewTargetedStaticInterference), "preconditions.first_target_resolution", "completed");
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.ReviewTargetedStaticInterference), "preconditions.second_target_resolution", "failed", "second_target_not_resolved");
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.ReviewTargetedStaticInterference), "verification.scope_check", "completed");
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.ReviewTargetedStaticInterference), "final", "completed", "status=completed");
    }

    private void ValidateUnsavedParentFailure(string letterHAssemblyPath, string replacementVerticalPath, string nestedTargetLeafName)
    {
        _ctx.Documents.NewDocument(SwDocType.Assembly);
        var insertedH = _ctx.Assembly.InsertComponent(letterHAssemblyPath, 0, 0, 0);
        string nestedTarget = insertedH.Name + "/" + insertedH.Name + "/" + nestedTargetLeafName;

        var workflow = new WorkflowService(_ctx.Documents, _ctx.Assembly, null, _ctx.ConnectionManager);
        var result = workflow.ReplaceNestedComponentAndVerifyPersistence(
            replacementVerticalPath,
            hierarchyPath: nestedTarget);

        Assert.Equal("parent_assembly_not_saved", result.Status);
        AssertCompatibilityAdvisoryMatchesRuntime(result.CompatibilityAdvisory);
        Assert.False(result.PersistenceVerified);
        Assert.Null(result.ParentAssemblyFilePath);
    }

    private HelloWorldAssemblyScenario CreateReusableHelloWorldAssemblyScenario(string outputDirectory, IReadOnlyDictionary<string, string> primitiveParts)
    {
        var letterAssemblies = new Dictionary<char, LetterAssemblyArtifact>
        {
            ['H'] = CreateLetterAssembly('H', primitiveParts, outputDirectory),
            ['E'] = CreateLetterAssembly('E', primitiveParts, outputDirectory),
            ['L'] = CreateLetterAssembly('L', primitiveParts, outputDirectory),
            ['O'] = CreateLetterAssembly('O', primitiveParts, outputDirectory),
            ['W'] = CreateLetterAssembly('W', primitiveParts, outputDirectory),
            ['R'] = CreateLetterAssembly('R', primitiveParts, outputDirectory),
            ['D'] = CreateLetterAssembly('D', primitiveParts, outputDirectory),
        };

        ValidateUnsavedParentFailure(
            letterAssemblies['H'].AssemblyPath,
            primitiveParts["V7R"],
            Path.GetFileNameWithoutExtension(primitiveParts["V7"]) + "-2");

        _ctx.Documents.NewDocument(SwDocType.Assembly);
        double cursorX = 0;
        ComponentInfo? insertedH = null;
        string? insertedTopLevelOName = null;
        foreach (char character in "HELLO WORLD")
        {
            if (character == ' ')
            {
                cursorX += WordGap;
                continue;
            }

            var assembly = letterAssemblies[character];
            var inserted = _ctx.Assembly.InsertComponent(assembly.AssemblyPath, cursorX, 0, 0);
            if (character == 'H' && insertedH == null)
            {
                insertedH = inserted;
            }

            if (character == 'O' && insertedTopLevelOName == null)
            {
                insertedTopLevelOName = inserted.Name;
            }

            cursorX += assembly.WidthMeters + LetterGap;
        }

        Assert.NotNull(insertedH);
        Assert.NotNull(insertedTopLevelOName);

        string parentAssemblyPath = GetArtifactPath(outputDirectory, "hello-world-parent.sldasm");
        var save = _ctx.Documents.SaveDocumentAs(parentAssemblyPath, sourcePath: null, saveAsCopy: false);
        Assert.Equal(Path.GetFullPath(parentAssemblyPath), save.OutputPath, StringComparer.OrdinalIgnoreCase);

        _ctx.Documents.OpenDocument(parentAssemblyPath);

        var topLevelFailureWorkflow = new WorkflowService(_ctx.Documents, _ctx.Assembly, null, _ctx.ConnectionManager);
        var topLevelFailure = topLevelFailureWorkflow.ReplaceNestedComponentAndVerifyPersistence(
            primitiveParts["V7R"],
            hierarchyPath: insertedTopLevelOName!);
        Assert.Equal("target_not_nested", topLevelFailure.Status);
        AssertCompatibilityAdvisoryMatchesRuntime(topLevelFailure.CompatibilityAdvisory);

        var recursiveComponents = _ctx.Assembly.ListComponentsRecursive();
        var hVerticalTargets = recursiveComponents
            .Where(component =>
                component.HierarchyPath.StartsWith(insertedH!.Name + "/", StringComparison.OrdinalIgnoreCase)
                && string.Equals(component.Path, primitiveParts["V7"], StringComparison.OrdinalIgnoreCase))
            .OrderBy(component => component.HierarchyPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Equal(2, hVerticalTargets.Count);

        var uniqueNested = recursiveComponents.Single(component => string.Equals(component.Path, primitiveParts["V3"], StringComparison.OrdinalIgnoreCase));
        var uniqueImpact = _ctx.Assembly.AnalyzeSharedPartEditImpact(hierarchyPath: uniqueNested.HierarchyPath);
        Assert.True(uniqueImpact.SafeDirectEdit);
        Assert.Equal("safe_direct_edit", uniqueImpact.RecommendedAction);

        var ambiguousResolution = _ctx.Assembly.ResolveComponentTarget(componentPath: primitiveParts["V7"]);
        Assert.True(ambiguousResolution.IsAmbiguous);
        Assert.False(ambiguousResolution.IsResolved);

        string targetHierarchyPath = hVerticalTargets[^1].HierarchyPath;
        var exactResolution = _ctx.Assembly.ResolveComponentTarget(hierarchyPath: targetHierarchyPath);
        Assert.True(exactResolution.IsResolved);

        int originalVerticalReuseCount = recursiveComponents.Count(component =>
            string.Equals(component.Path, primitiveParts["V7"], StringComparison.OrdinalIgnoreCase));
        Assert.True(originalVerticalReuseCount >= 10);

        return new HelloWorldAssemblyScenario(
            parentAssemblyPath,
            primitiveParts["V7"],
            primitiveParts["V7R"],
            primitiveParts["V3"],
            targetHierarchyPath,
            insertedTopLevelOName!,
            originalVerticalReuseCount);
    }

    private IReadOnlyList<string> ExportVerificationViews(string documentPath, string outputDirectory, string prefix)
    {
        _ctx.Documents.OpenDocument(documentPath);

        var exportedPaths = new List<string>(VerificationViews.Length);
        foreach (var (view, suffix) in VerificationViews)
        {
            _ctx.Documents.ShowStandardView(view);
            string outputPath = GetArtifactPath(outputDirectory, $"{prefix}-{suffix}.png");
            var export = _ctx.Documents.ExportCurrentViewPng(outputPath, 1600, 900, false);

            Assert.True(File.Exists(outputPath), $"Expected exported image to exist: {outputPath}");
            Assert.True(new FileInfo(outputPath).Length > 0, $"Expected exported image to be non-empty: {outputPath}");
            Assert.Equal(outputPath, export.OutputPath, StringComparer.OrdinalIgnoreCase);
            exportedPaths.Add(outputPath);
        }

        return exportedPaths.AsReadOnly();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_RuntimeSupportMatchesCompiledSupportMatrix()
    {
        var compatibility = _ctx.ConnectionManager.GetCompatibilityInfo();
        var supportMatrix = SwConnectionManager.GetCompiledSupportMatrix();

        CrossVersionSmokeSuite.AssertRuntimeClassificationIsConsistent(compatibility);
        AssertRuntimeSupportMatchesMatrix(compatibility, supportMatrix);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_CrossVersionSmokeSuite_ExercisesCompatibilityAndDocumentSurface()
    {
        string outputDirectory = GetArtifactDirectory("cross-version-smoke-document-surface");
        var (compatibility, expectation) = GetSmokeContext();

        if (!expectation.ShouldRunDirectMutationSmoke)
        {
            var currentOpenDocuments = _ctx.Documents.ListDocuments();
            var currentActiveDocument = _ctx.Documents.GetActiveDocument();

            string blockedReportPath = WriteSmokeReport(
                outputDirectory,
                "cross-version-smoke-document-surface.json",
                compatibility,
                expectation,
                new CrossVersionSmokeAreaReport(
                    "compatibility-and-document-surface",
                    "blocked-by-runtime-policy",
                    "Validated runtime compatibility metadata and current document visibility, then skipped live sketch/export construction because this runtime is outside the direct-mutation smoke window.",
                    new[]
                    {
                        "GetSolidWorksCompatibility",
                        "ListDocuments",
                        "GetActiveDocument",
                    },
                    currentActiveDocument is null
                        ? Array.Empty<string>()
                        : new[] { currentActiveDocument.Path ?? currentActiveDocument.Title }));

            Assert.NotNull(currentOpenDocuments);
            Assert.True(File.Exists(blockedReportPath));
            return;
        }

        string sketchSheetPath = CreateSketchCapabilitySheet(outputDirectory);
        Assert.True(File.Exists(sketchSheetPath));

        _ctx.Documents.OpenDocument(sketchSheetPath);
        var activeDocument = _ctx.Documents.GetActiveDocument();
        Assert.NotNull(activeDocument);
        Assert.Equal(sketchSheetPath, activeDocument!.Path, StringComparer.OrdinalIgnoreCase);

        Assert.True(_ctx.Selection.ListReferencePlanes().Count >= 3, "Expected default reference planes to be discoverable for smoke coverage.");

        var openDocuments = _ctx.Documents.ListDocuments();
        Assert.Contains(openDocuments, document => string.Equals(document.Path, sketchSheetPath, StringComparison.OrdinalIgnoreCase));

        var editState = _ctx.Selection.GetEditState();
        Assert.False(editState.IsEditing);
        Assert.True(editState.CanReadFeatureTree);

        _ctx.Documents.RotateView(12, -8, 0);
        string pngPath = GetArtifactPath(outputDirectory, "cross-version-smoke-document-surface.png");
        var pngExport = _ctx.Documents.ExportCurrentViewPng(pngPath, 1600, 900, false);
        Assert.True(File.Exists(pngPath), $"Expected exported image to exist: {pngPath}");
        Assert.Equal(Path.GetFullPath(pngPath), pngExport.OutputPath, StringComparer.OrdinalIgnoreCase);

        var save = _ctx.Documents.SaveDocument(sketchSheetPath);
        Assert.Equal(Path.GetFullPath(sketchSheetPath), save.OutputPath, StringComparer.OrdinalIgnoreCase);

        string stepPath = GetArtifactPath(outputDirectory, "cross-version-smoke-document-surface.step");
        var stepExport = _ctx.Documents.SaveDocumentAs(stepPath, sourcePath: null, saveAsCopy: true);
        Assert.True(File.Exists(stepPath), $"Expected exported STEP file to exist: {stepPath}");
        Assert.Equal(Path.GetFullPath(stepPath), stepExport.OutputPath, StringComparer.OrdinalIgnoreCase);

        var featureDiagnostics = _ctx.Selection.GetFeatureDiagnostics();
        Assert.Equal(0, featureDiagnostics.ErrorCount);
        Assert.Empty(featureDiagnostics.CorrelatedIssues ?? Array.Empty<CorrelatedDiagnosticIssueInfo>());

        string reportPath = WriteSmokeReport(
            outputDirectory,
            "cross-version-smoke-document-surface.json",
            compatibility,
            expectation,
            new CrossVersionSmokeAreaReport(
                "compatibility-and-document-surface",
                "completed",
                "Validated runtime compatibility metadata plus document, selection, sketch, export, and diagnostics surfaces against a real SolidWorks session.",
                new[]
                {
                    "GetSolidWorksCompatibility",
                    "ListReferencePlanes",
                    "SelectByName",
                    "InsertSketch",
                    "AddPoint",
                    "AddLine",
                    "AddArc",
                    "AddEllipse",
                    "AddPolygon",
                    "AddText",
                    "AddCircle",
                    "AddRectangle",
                    "FinishSketch",
                    "OpenDocument",
                    "GetActiveDocument",
                    "ListDocuments",
                    "RotateView",
                    "ExportCurrentViewPng",
                    "SaveDocument",
                    "SaveDocumentAs",
                    "GetEditState",
                    "GetFeatureDiagnostics",
                },
                new[]
                {
                    sketchSheetPath,
                    pngPath,
                    stepPath,
                }));

        Assert.True(File.Exists(reportPath));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_CrossVersionSmokeSuite_ExercisesAssemblyAndInterferenceSurface()
    {
        string outputDirectory = GetArtifactDirectory("cross-version-smoke-assembly-surface");
        var (compatibility, expectation) = GetSmokeContext();

        if (!expectation.ShouldRunDirectMutationSmoke)
        {
            string blockedReportPath = WriteSmokeReport(
                outputDirectory,
                "cross-version-smoke-assembly-surface.json",
                compatibility,
                expectation,
                new CrossVersionSmokeAreaReport(
                    "assembly-and-interference-surface",
                    "blocked-by-runtime-policy",
                    "Skipped the live assembly construction sample because this runtime is outside the direct-mutation smoke window.",
                    new[]
                    {
                        "InsertComponent",
                        "ListComponents",
                        "ListComponentsRecursive",
                        "MeasureEntities",
                        "AddMateCoincident",
                        "AddMateDistance",
                        "CheckInterference",
                        "ReviewTargetedStaticInterference",
                    },
                    Array.Empty<string>()));

            Assert.True(File.Exists(blockedReportPath));
            return;
        }

        var primitiveParts = CreatePrimitivePartLibrary(outputDirectory);
        ValidateScratchAssemblyCapabilities(primitiveParts["V7"]);

        string reportPath = WriteSmokeReport(
            outputDirectory,
            "cross-version-smoke-assembly-surface.json",
            compatibility,
            expectation,
            new CrossVersionSmokeAreaReport(
                "assembly-and-interference-surface",
                "completed",
                "Validated reusable-part assembly creation, exact component listing, mate-driven separation, measurement, and targeted static interference review.",
                new[]
                {
                    "NewDocument",
                    "InsertComponent",
                    "ListComponents",
                    "ListComponentsRecursive",
                    "MeasureEntities",
                    "AddMateCoincident",
                    "AddMateDistance",
                    "CheckInterference",
                    "ReviewTargetedStaticInterference",
                },
                primitiveParts.Values.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()));

        Assert.True(File.Exists(reportPath));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Integration_CrossVersionSmokeSuite_ExercisesReplacementAndDiagnosticsSurface()
    {
        string outputDirectory = GetArtifactDirectory("cross-version-smoke-replacement-surface");
        var (compatibility, expectation) = GetSmokeContext();

        if (!expectation.ShouldRunHighRiskWorkflowSmoke)
        {
            string blockedReportPath = WriteSmokeReport(
                outputDirectory,
                "cross-version-smoke-replacement-surface.json",
                compatibility,
                expectation,
                new CrossVersionSmokeAreaReport(
                    "replacement-and-diagnostics-surface",
                    "blocked-by-runtime-policy",
                    "Skipped the nested replacement workflow sample because this runtime is outside the high-risk workflow smoke window.",
                    new[]
                    {
                        "AnalyzeSharedPartEditImpact",
                        "ReplaceNestedComponentAndVerifyPersistence",
                        "ListComponentsRecursive",
                        "CheckInterference",
                        "DiagnoseActiveDocumentHealth",
                    },
                    Array.Empty<string>()));

            Assert.True(File.Exists(blockedReportPath));
            return;
        }

        var primitiveParts = CreatePrimitivePartLibrary(outputDirectory);
        var setup = CreateReusableHelloWorldAssemblyScenario(outputDirectory, primitiveParts);

        var beforeInterference = _ctx.Assembly.CheckInterference(treatCoincidenceAsInterference: false);
        Assert.False(beforeInterference.HasInterference, "HELLO WORLD engineering assembly should be interference-free before replacement.");

        int initialWorkflowLogCount = ServerLogBuffer.GetSnapshot().Count;
        var workflowLogger = new ServerLogWorkflowStageLogger();
        var workflow = new WorkflowService(_ctx.Documents, _ctx.Assembly, null, _ctx.ConnectionManager, workflowLogger);
        var noOpResult = workflow.ReplaceNestedComponentAndVerifyPersistence(
            setup.OriginalVerticalPartPath,
            hierarchyPath: setup.TargetHierarchyPath);
        Assert.Equal("replacement_matches_source_file", noOpResult.Status);
        AssertCompatibilityAdvisoryMatchesRuntime(noOpResult.CompatibilityAdvisory);

        var beforeExports = ExportVerificationViews(setup.ParentAssemblyPath, outputDirectory, "hello-world-engineering-before");

        var result = workflow.ReplaceNestedComponentAndVerifyPersistence(
            setup.ReplacementVerticalPartPath,
            hierarchyPath: setup.TargetHierarchyPath);

        var afterExports = ExportVerificationViews(setup.ParentAssemblyPath, outputDirectory, "hello-world-engineering-after");
        var recursiveComponents = _ctx.Assembly.ListComponentsRecursive();
        var postInterference = _ctx.Assembly.CheckInterference(treatCoincidenceAsInterference: false);
        var healthDiagnostics = new WorkflowService(_ctx.Documents, _ctx.Assembly, _ctx.Selection, _ctx.ConnectionManager, workflowLogger)
            .DiagnoseActiveDocumentHealth(forceRebuild: true, topOnly: false, saveDocument: true);

        Assert.Equal("completed", result.Status);
        Assert.True(result.PersistenceVerified);
        Assert.NotNull(result.PersistenceResolution);
        Assert.NotNull(result.PersistenceResolution!.ResolvedInstance);
        Assert.Equal(setup.ReplacementVerticalPartPath, result.PersistenceResolution!.ResolvedInstance!.Path, StringComparer.OrdinalIgnoreCase);
        var postReplacementImpact = _ctx.Assembly.AnalyzeSharedPartEditImpact(hierarchyPath: result.PersistenceResolution.ResolvedInstance!.HierarchyPath);
        Assert.False(result.PreReplacementImpactAnalysis.SafeDirectEdit);
        Assert.True(result.PreReplacementImpactAnalysis.AffectedInstanceCount >= setup.OriginalVerticalReuseCount);
        Assert.NotNull(result.PostReplacementImpactAnalysis);
        Assert.True(result.PostReplacementImpactAnalysis!.SafeDirectEdit);
        Assert.Equal(1, result.PostReplacementImpactAnalysis.AffectedInstanceCount);
        Assert.True(postReplacementImpact.SafeDirectEdit);
        Assert.Equal("safe_direct_edit", postReplacementImpact.RecommendedAction);
        Assert.False(postInterference.HasInterference, "Replacement should preserve a non-interfering HELLO WORLD engineering assembly.");
        Assert.Equal("completed", healthDiagnostics.Status);
        AssertCompatibilityAdvisoryMatchesRuntime(result.CompatibilityAdvisory);
        AssertCompatibilityAdvisoryMatchesRuntime(healthDiagnostics.CompatibilityAdvisory);
        Assert.NotNull(healthDiagnostics.ActiveDocument);
        Assert.Equal(setup.ParentAssemblyPath, healthDiagnostics.ActiveDocument!.Path, StringComparer.OrdinalIgnoreCase);
        Assert.True(healthDiagnostics.Rebuild.RebuildAttempted);
        Assert.False(healthDiagnostics.HasBlockingIssues);
        Assert.False(healthDiagnostics.HasWarnings);
        Assert.True(healthDiagnostics.ReadyForVerificationGate);
        Assert.True(healthDiagnostics.SaveHealth.SaveAttempted);
        Assert.True(healthDiagnostics.SaveHealth.SaveSucceeded);
        Assert.NotNull(healthDiagnostics.SaveHealth.SaveResult);
        Assert.NotNull(healthDiagnostics.FeatureDiagnosticsAfterRebuild);
        Assert.NotNull(healthDiagnostics.ActionableDiagnostics);
        Assert.Equal(0, healthDiagnostics.FeatureDiagnosticsAfterRebuild!.WarningCount);
        Assert.Empty(healthDiagnostics.FeatureDiagnosticsAfterRebuild.WhatsWrongItems);
        Assert.Empty(healthDiagnostics.FeatureDiagnosticsAfterRebuild.CorrelatedIssues ?? Array.Empty<CorrelatedDiagnosticIssueInfo>());
        Assert.Empty(healthDiagnostics.ActionableDiagnostics!.CurrentIssues);
        Assert.Empty(healthDiagnostics.ActionableDiagnostics.ResolvedByRebuildIssues);
        Assert.Empty(healthDiagnostics.ActionableDiagnostics.IntroducedByRebuildIssues);

        _ctx.Documents.OpenDocument(setup.OriginalVerticalPartPath);
        var originalVerticalFeatureTree = _ctx.Selection.ListFeatureTree();
        var destructiveFeature = originalVerticalFeatureTree
            .FirstOrDefault(feature =>
                !feature.IsSketch
                && (feature.TypeName.Contains("Extrusion", StringComparison.OrdinalIgnoreCase)
                    || feature.TypeName.Contains("Extrude", StringComparison.OrdinalIgnoreCase)
                    || feature.TypeName.Contains("Fillet", StringComparison.OrdinalIgnoreCase)))
            ?? throw new Xunit.Sdk.XunitException("Expected the reusable V7 part to expose at least one deletable solid feature.");
        var deleteFeatureResult = _ctx.Selection.DeleteFeatureByName(destructiveFeature.Name);
        Assert.True(deleteFeatureResult.Success, deleteFeatureResult.Message);
        var brokenPartSave = _ctx.Documents.SaveDocument(setup.OriginalVerticalPartPath);
        Assert.Equal(setup.OriginalVerticalPartPath, brokenPartSave.OutputPath, StringComparer.OrdinalIgnoreCase);

        _ctx.Documents.OpenDocument(setup.ParentAssemblyPath);
        var brokenHealthDiagnostics = new WorkflowService(_ctx.Documents, _ctx.Assembly, _ctx.Selection, _ctx.ConnectionManager, workflowLogger)
            .DiagnoseActiveDocumentHealth(forceRebuild: true, topOnly: false, saveDocument: false);

        Assert.Equal("completed", brokenHealthDiagnostics.Status);
        Assert.NotNull(brokenHealthDiagnostics.ActiveDocument);
        Assert.Equal(setup.ParentAssemblyPath, brokenHealthDiagnostics.ActiveDocument!.Path, StringComparer.OrdinalIgnoreCase);
        Assert.True(
            brokenHealthDiagnostics.HasBlockingIssues
            || brokenHealthDiagnostics.HasWarnings
            || brokenHealthDiagnostics.ActionableDiagnostics?.CurrentIssues.Count > 0);
        Assert.NotNull(brokenHealthDiagnostics.FeatureDiagnosticsAfterRebuild);
        Assert.NotEmpty(brokenHealthDiagnostics.FeatureDiagnosticsAfterRebuild!.CorrelatedIssues ?? Array.Empty<CorrelatedDiagnosticIssueInfo>());
        Assert.NotNull(brokenHealthDiagnostics.ActionableDiagnostics);
        Assert.NotEmpty(brokenHealthDiagnostics.ActionableDiagnostics!.CurrentIssues);
        Assert.True(
            brokenHealthDiagnostics.ActionableDiagnostics.BlockingIssues.Count > 0
            || brokenHealthDiagnostics.ActionableDiagnostics.WarningIssues.Count > 0);
        Assert.Contains(brokenHealthDiagnostics.ActionableDiagnostics.CurrentIssues, issue =>
            string.Equals(issue.TargetContext.ScopeType, "component_instance", StringComparison.OrdinalIgnoreCase)
            && string.Equals(issue.TargetContext.DocumentPath, setup.ParentAssemblyPath, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(issue.TargetContext.HierarchyPath)
            && !string.IsNullOrWhiteSpace(issue.TargetContext.ComponentName));

        Assert.Equal(4, beforeExports.Count);
        Assert.Equal(4, afterExports.Count);
        Assert.Single(recursiveComponents.Where(component =>
            string.Equals(component.Path, setup.ReplacementVerticalPartPath, StringComparison.OrdinalIgnoreCase)));
        Assert.True(recursiveComponents.Count(component =>
            string.Equals(component.Path, setup.OriginalVerticalPartPath, StringComparison.OrdinalIgnoreCase)) >= setup.OriginalVerticalReuseCount - 1);

        var workflowEntries = ServerLogBuffer.GetSnapshot().Skip(initialWorkflowLogCount).ToArray();
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "preconditions.target_validation", "failed", "replacement_matches_source_file");
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "mutation.replace_component", "completed");
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.ReplaceNestedComponentAndVerifyPersistence), "verification.persistence_resolution", "completed", "resolved=true");
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.DiagnoseActiveDocumentHealth), "verification.save_document", "completed");
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.DiagnoseActiveDocumentHealth), "verification.feature_diagnostics_after_rebuild", "completed", "correlatedIssues=");
        AssertWorkflowLogEntry(workflowEntries, nameof(WorkflowService.DiagnoseActiveDocumentHealth), "final", "completed", "status=completed");

        string reportPath = WriteSmokeReport(
            outputDirectory,
            "cross-version-smoke-replacement-surface.json",
            compatibility,
            expectation,
            new CrossVersionSmokeAreaReport(
                "replacement-and-diagnostics-surface",
                "completed",
                "Validated the nested replacement workflow, persistence verification, shared-part impact analysis, viewport exports, healthy post-edit diagnostics, and a deliberately damaged shared HELLO WORLD source part that returned actionable native diagnostics.",
                new[]
                {
                    "AnalyzeSharedPartEditImpact",
                    "ResolveComponentTarget",
                    "ReplaceNestedComponentAndVerifyPersistence",
                    "DeleteFeatureByName",
                    "SaveDocument",
                    "ListComponentsRecursive",
                    "CheckInterference",
                    "ExportCurrentViewPng",
                    "DiagnoseActiveDocumentHealth",
                },
                beforeExports
                    .Concat(afterExports)
                    .Append(setup.ParentAssemblyPath)
                    .Append(setup.OriginalVerticalPartPath)
                    .ToArray()));

        Assert.True(File.Exists(reportPath));
    }

    private static void AssertWorkflowLogEntry(
        IReadOnlyList<ServerLogEntry> entries,
        string workflowName,
        string stageName,
        string boundary,
        string? detailContains = null)
    {
        Assert.Contains(entries, entry =>
            string.Equals(entry.Source, "Workflow", StringComparison.Ordinal)
            && entry.Message.Contains($"{workflowName} | stage={stageName} | boundary={boundary}", StringComparison.Ordinal)
            && (detailContains == null || entry.Message.Contains(detailContains, StringComparison.OrdinalIgnoreCase)));
    }
}
