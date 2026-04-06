using SolidWorksBridge.SolidWorks;

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
            ],
            UseAssemblyMates: true),
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
        new("V7R", "bar-v7-replacement.sldprt", PrimitivePartShape.BarWithDualTopHoles, UnitSize, UnitSize * 7, PartDepth),
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

    private enum PrimitivePartShape
    {
        BarPlain,
        BarWithTopHole,
        BarWithDualTopHoles,
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

            case PrimitivePartShape.BarWithDualTopHoles:
                SelectPartFace(Axis.Z, true);
                _ctx.Sketch.InsertSketch();
                _ctx.Sketch.AddCircle(0, spec.Height / 2.0 - UnitSize, UnitSize * 0.25);
                _ctx.Sketch.AddCircle(0, spec.Height / 2.0 - (UnitSize * 2.5), UnitSize * 0.2);
                _ctx.Sketch.FinishSketch();
                Assert.Equal("ExtrudeCut", _ctx.Feature.ExtrudeCut(spec.Depth * 3, EndCondition.ThroughAll).Type);
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
        Assert.Equal(2, components.Count);
        Assert.NotEqual(first.Name, second.Name);

        var interference = _ctx.Assembly.CheckInterference([first.Name, second.Name]);
        Assert.True(interference.HasInterference);
        Assert.Equal(2, interference.CheckedComponentCount);

        AddDistanceMate(first.Name, Axis.X, true, second.Name, Axis.X, false, UnitSize);
        var separated = _ctx.Assembly.CheckInterference([first.Name, second.Name]);
        Assert.False(separated.HasInterference, "Distance mate should separate the scratch components and remove interference.");
    }

    private void ValidateUnsavedParentFailure(string letterHAssemblyPath, string replacementVerticalPath, string nestedTargetLeafName)
    {
        _ctx.Documents.NewDocument(SwDocType.Assembly);
        var insertedH = _ctx.Assembly.InsertComponent(letterHAssemblyPath, 0, 0, 0);
        string nestedTarget = insertedH.Name + "/" + insertedH.Name + "/" + nestedTargetLeafName;

        var workflow = new WorkflowService(_ctx.Documents, _ctx.Assembly);
        var result = workflow.ReplaceNestedComponentAndVerifyPersistence(
            replacementVerticalPath,
            hierarchyPath: nestedTarget);

        Assert.Equal("parent_assembly_not_saved", result.Status);
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

        var topLevelFailureWorkflow = new WorkflowService(_ctx.Documents, _ctx.Assembly);
        var topLevelFailure = topLevelFailureWorkflow.ReplaceNestedComponentAndVerifyPersistence(
            primitiveParts["V7R"],
            hierarchyPath: insertedTopLevelOName!);
        Assert.Equal("target_not_nested", topLevelFailure.Status);

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
    public void Integration_HelloWorldEngineeringWorkflow_ExercisesCoreCapabilities()
    {
        string outputDirectory = GetArtifactDirectory("hello-world-engineering-workflow");

        string sketchSheetPath = CreateSketchCapabilitySheet(outputDirectory);
        Assert.True(File.Exists(sketchSheetPath));

        var primitiveParts = CreatePrimitivePartLibrary(outputDirectory);
        ValidateScratchAssemblyCapabilities(primitiveParts["V7"]);

        var setup = CreateReusableHelloWorldAssemblyScenario(outputDirectory, primitiveParts);

        var beforeInterference = _ctx.Assembly.CheckInterference(treatCoincidenceAsInterference: false);
        Assert.False(beforeInterference.HasInterference, "HELLO WORLD engineering assembly should be interference-free before replacement.");

        var sameFileWorkflow = new WorkflowService(_ctx.Documents, _ctx.Assembly);
        var noOpResult = sameFileWorkflow.ReplaceNestedComponentAndVerifyPersistence(
            setup.OriginalVerticalPartPath,
            hierarchyPath: setup.TargetHierarchyPath);
        Assert.Equal("replacement_matches_source_file", noOpResult.Status);

        var beforeExports = ExportVerificationViews(setup.ParentAssemblyPath, outputDirectory, "hello-world-engineering-before");

        var workflow = new WorkflowService(_ctx.Documents, _ctx.Assembly);
        var result = workflow.ReplaceNestedComponentAndVerifyPersistence(
            setup.ReplacementVerticalPartPath,
            hierarchyPath: setup.TargetHierarchyPath);

        var afterExports = ExportVerificationViews(setup.ParentAssemblyPath, outputDirectory, "hello-world-engineering-after");
        var recursiveComponents = _ctx.Assembly.ListComponentsRecursive();
        var postInterference = _ctx.Assembly.CheckInterference(treatCoincidenceAsInterference: false);
        var postReplacementImpact = _ctx.Assembly.AnalyzeSharedPartEditImpact(hierarchyPath: result.PersistenceResolution!.ResolvedInstance!.HierarchyPath);

        Assert.Equal("completed", result.Status);
        Assert.True(result.PersistenceVerified);
        Assert.NotNull(result.PersistenceResolution);
        Assert.Equal(setup.ReplacementVerticalPartPath, result.PersistenceResolution!.ResolvedInstance!.Path, StringComparer.OrdinalIgnoreCase);
        Assert.False(result.PreReplacementImpactAnalysis.SafeDirectEdit);
        Assert.True(result.PreReplacementImpactAnalysis.AffectedInstanceCount >= setup.OriginalVerticalReuseCount);
        Assert.NotNull(result.PostReplacementImpactAnalysis);
        Assert.True(result.PostReplacementImpactAnalysis!.SafeDirectEdit);
        Assert.Equal(1, result.PostReplacementImpactAnalysis.AffectedInstanceCount);
        Assert.True(postReplacementImpact.SafeDirectEdit);
        Assert.Equal("safe_direct_edit", postReplacementImpact.RecommendedAction);
        Assert.False(postInterference.HasInterference, "Replacement should preserve a non-interfering HELLO WORLD engineering assembly.");

        Assert.Equal(4, beforeExports.Count);
        Assert.Equal(4, afterExports.Count);
        Assert.Single(recursiveComponents.Where(component =>
            string.Equals(component.Path, setup.ReplacementVerticalPartPath, StringComparison.OrdinalIgnoreCase)));
        Assert.True(recursiveComponents.Count(component =>
            string.Equals(component.Path, setup.OriginalVerticalPartPath, StringComparison.OrdinalIgnoreCase)) >= setup.OriginalVerticalReuseCount - 1);
    }
}