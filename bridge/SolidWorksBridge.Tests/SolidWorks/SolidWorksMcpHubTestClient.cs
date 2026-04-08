using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

internal sealed class SolidWorksMcpHubTestClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string[] RequiredTools =
    [
        "Ping",
        "SolidWorksConnect",
        "SolidWorksDisconnect",
        "GetSolidWorksCompatibility",
        "GetSolidWorksSupportMatrix",
        "NewDocument",
        "OpenDocument",
        "CloseDocument",
        "SaveDocument",
        "GetActiveDocumentRebuildState",
        "ForceRebuildActiveDocument",
        "SaveDocumentAs",
        "Undo",
        "ShowStandardView",
        "RotateView",
        "ExportCurrentViewPng",
        "ListDocuments",
        "GetActiveDocument",
        "SelectByName",
        "ListEntities",
        "ListReferencePlanes",
        "GetSolidWorksContext",
        "GetEditState",
        "GetFeatureDiagnostics",
        "SelectEntity",
        "MeasureEntities",
        "DeleteFeatureByName",
        "DeleteUnusedSketches",
        "ClearSelection",
        "InsertSketch",
        "FinishSketch",
        "SketchUseEdge3",
        "AddPoint",
        "AddLine",
        "AddCircle",
        "AddRectangle",
        "AddArc",
        "AddEllipse",
        "AddPolygon",
        "AddText",
        "Extrude",
        "ExtrudeCut",
        "Revolve",
        "Fillet",
        "Chamfer",
        "Shell",
        "InsertComponent",
        "AddMateCoincident",
        "AddMateConcentric",
        "AddMateParallel",
        "AddMateDistance",
        "AddMateAngle",
        "ListComponents",
        "ListComponentsRecursive",
        "ResolveComponentTarget",
        "AnalyzeSharedPartEditImpact",
        "CheckInterference",
        "ReplaceComponent",
        "DiagnoseActiveDocumentHealth",
        "ReplaceNestedComponentAndVerifyPersistence",
        "ReviewTargetedStaticInterference",
    ];

    private readonly StdioClientTransport _transport;
    private readonly McpClient _client;

    private SolidWorksMcpHubTestClient(StdioClientTransport transport, McpClient client)
    {
        _transport = transport;
        _client = client;
    }

    public static SolidWorksMcpHubTestClient Create(string clientName = "SolidWorksBridge.Tests")
    {
        string exePath = ResolveAppExecutablePath();
        var transport = new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = "solidworks-mcp-test-proxy",
                Command = exePath,
                Arguments = ["--proxy", "--client", clientName],
                WorkingDirectory = Path.GetDirectoryName(exePath),
            },
            NullLoggerFactory.Instance);

        var client = McpClient.CreateAsync(transport, loggerFactory: NullLoggerFactory.Instance)
            .GetAwaiter()
            .GetResult();

        var session = new SolidWorksMcpHubTestClient(transport, client);
        session.ValidateRequiredTools();
        return session;
    }

    public void Dispose()
    {
        _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public string CallToolText(string toolName, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        string resolvedToolName = ToMcpToolName(toolName);
        var result = _client.CallToolAsync(resolvedToolName, arguments).GetAwaiter().GetResult();
        return ExtractText(resolvedToolName, result);
    }

    public T CallTool<T>(string toolName, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        string text = CallToolText(toolName, arguments);
        if (string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
        {
            return default!;
        }

        var value = JsonSerializer.Deserialize<T>(text, JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException($"Tool '{toolName}' returned an empty payload when '{typeof(T).Name}' was expected.");
        }

        return value;
    }

    public T? CallToolNullable<T>(string toolName, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        string text = CallToolText(toolName, arguments);
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(text, JsonOptions);
    }

    public string CallToolErrorText(string toolName, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        string resolvedToolName = ToMcpToolName(toolName);
        var result = _client.CallToolAsync(resolvedToolName, arguments).GetAwaiter().GetResult();
        string text = GetResultText(result);
        if (result.IsError.GetValueOrDefault())
        {
            return text;
        }

        throw new InvalidOperationException(
            $"Tool '{resolvedToolName}' completed successfully when an MCP error result was expected. Payload: {text}");
    }

    public static IReadOnlyDictionary<string, object?> Args(params (string Key, object? Value)[] values)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (value is not null)
            {
                dictionary[key] = value;
            }
        }

        return dictionary;
    }

    private void ValidateRequiredTools()
    {
        var tools = _client.ListToolsAsync().GetAwaiter().GetResult();
        var toolNames = new HashSet<string>(tools.Select(tool => tool.Name), StringComparer.Ordinal);
        string[] missing = RequiredTools
            .Select(ToMcpToolName)
            .Where(name => !toolNames.Contains(name))
            .ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        string available = toolNames.Count == 0
            ? "<none>"
            : string.Join(", ", toolNames.OrderBy(name => name, StringComparer.Ordinal));

        throw new InvalidOperationException(
            "The running SolidWorks MCP hub does not expose the tools required by the integration harness. " +
            $"Missing tools: {string.Join(", ", missing)}. " +
            $"Available tools: {available}. " +
            "If you already had the hub running before rebuilding, restart SolidWorksMcpApp so the latest tool surface is loaded.");
    }

    private static string ToMcpToolName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name must not be empty.", nameof(name));
        }

        if (name.Contains('_', StringComparison.Ordinal))
        {
            return name.ToLowerInvariant();
        }

        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];
            if (char.IsUpper(current))
            {
                bool hasPrevious = index > 0;
                bool previousIsLowerOrDigit = hasPrevious && (char.IsLower(name[index - 1]) || char.IsDigit(name[index - 1]));
                bool nextIsLower = index + 1 < name.Length && char.IsLower(name[index + 1]);
                if (hasPrevious && (previousIsLowerOrDigit || nextIsLower))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }

    private static string ExtractText(string toolName, CallToolResult result)
    {
        string text = GetResultText(result);
        if (!result.IsError.GetValueOrDefault())
        {
            return text;
        }

        throw new InvalidOperationException(
            $"Tool '{toolName}' returned an MCP error result: {text}");
    }

    private static string GetResultText(CallToolResult result)
    {
        string[] textBlocks = result.Content
            .OfType<TextContentBlock>()
            .Select(block => block.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        string text = textBlocks.Length == 0
            ? string.Empty
            : string.Join(Environment.NewLine, textBlocks);
        return text;
    }

    private static string ResolveAppExecutablePath()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("SOLIDWORKS_MCP_APP_EXE");
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        foreach (var process in Process.GetProcessesByName("SolidWorksMcpApp"))
        {
            try
            {
                string? processPath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
                {
                    return Path.GetFullPath(processPath);
                }
            }
            catch
            {
                // Best effort only; keep searching other candidates.
            }
            finally
            {
                process.Dispose();
            }
        }

        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string[] candidates =
        [
            Path.Combine(repositoryRoot, "artifacts", "self-hosted", "win-x64", "SolidWorksMcpApp.exe"),
            Path.Combine(repositoryRoot, "app", "SolidWorksMcpApp", "bin", "Release", "net8.0-windows", "win-x64", "SolidWorksMcpApp.exe"),
            Path.Combine(repositoryRoot, "app", "SolidWorksMcpApp", "bin", "Debug", "net8.0-windows", "win-x64", "SolidWorksMcpApp.exe"),
        ];

        var existingCandidate = candidates
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .FirstOrDefault();

        if (existingCandidate is not null)
        {
            return existingCandidate.FullName;
        }

        throw new FileNotFoundException(
            "Could not locate SolidWorksMcpApp.exe for the hub/proxy integration tests. " +
            "Set SOLIDWORKS_MCP_APP_EXE or build the tray app first.");
    }
}

internal sealed class McpSwConnectionManager : ISwConnectionManager
{
    private sealed record ConnectToolResult(
        bool Connected,
        SolidWorksConnectionAttemptInfo? ConnectionAttempt,
        SolidWorksCompatibilityInfo? Compatibility);

    private readonly SolidWorksMcpHubTestClient _client;

    public McpSwConnectionManager(SolidWorksMcpHubTestClient client)
    {
        _client = client;
    }

    public bool IsConnected { get; private set; }

    public ISldWorksApp? SwApp => null;

    public SolidWorksConnectionAttemptInfo? LastConnectionAttempt { get; private set; }

    public void Connect()
    {
        var result = _client.CallTool<ConnectToolResult>("SolidWorksConnect");
        IsConnected = result.Connected;
        LastConnectionAttempt = result.ConnectionAttempt;
    }

    public void Disconnect()
    {
        _client.CallToolText("SolidWorksDisconnect");
        IsConnected = false;
        LastConnectionAttempt = null;
    }

    public void EnsureConnected() => Connect();

    public SolidWorksCompatibilityInfo GetCompatibilityInfo()
    {
        EnsureConnected();
        return _client.CallTool<SolidWorksCompatibilityInfo>("GetSolidWorksCompatibility");
    }
}

internal sealed class McpDocumentService : IDocumentService
{
    private readonly SolidWorksMcpHubTestClient _client;

    public McpDocumentService(SolidWorksMcpHubTestClient client)
    {
        _client = client;
    }

    public SwDocumentInfo NewDocument(SwDocType docType, string? templatePath = null)
        => _client.CallTool<SwDocumentInfo>("NewDocument", SolidWorksMcpHubTestClient.Args(
            ("type", docType.ToString()),
            ("templatePath", templatePath)));

    public SwOpenResult OpenDocument(string path)
        => _client.CallTool<SwOpenResult>("OpenDocument", SolidWorksMcpHubTestClient.Args(("path", path)));

    public void CloseDocument(string path)
        => _client.CallToolText("CloseDocument", SolidWorksMcpHubTestClient.Args(("path", path)));

    public SwSaveResult SaveDocument(string path)
        => _client.CallTool<SwSaveResult>("SaveDocument", SolidWorksMcpHubTestClient.Args(("path", path)));

    public RebuildStateInfo GetActiveDocumentRebuildState()
        => _client.CallTool<RebuildStateInfo>("GetActiveDocumentRebuildState");

    public RebuildExecutionResult ForceRebuildActiveDocument(bool topOnly = false)
        => _client.CallTool<RebuildExecutionResult>("ForceRebuildActiveDocument", SolidWorksMcpHubTestClient.Args(("topOnly", topOnly)));

    public SwSaveResult SaveDocumentAs(string outputPath, string? sourcePath = null, bool saveAsCopy = true)
        => _client.CallTool<SwSaveResult>("SaveDocumentAs", SolidWorksMcpHubTestClient.Args(
            ("outputPath", outputPath),
            ("sourcePath", sourcePath),
            ("saveAsCopy", saveAsCopy)));

    public void Undo(int steps = 1)
        => _client.CallToolText("Undo", SolidWorksMcpHubTestClient.Args(("steps", steps)));

    public void ShowStandardView(SwStandardView view)
        => _client.CallToolText("ShowStandardView", SolidWorksMcpHubTestClient.Args(("view", view.ToString())));

    public void RotateView(double xDegrees = 0, double yDegrees = 0, double zDegrees = 0)
        => _client.CallToolText("RotateView", SolidWorksMcpHubTestClient.Args(
            ("xDegrees", xDegrees),
            ("yDegrees", yDegrees),
            ("zDegrees", zDegrees)));

    public SwImageExportResult ExportCurrentViewPng(string outputPath, int width = 1600, int height = 900, bool includeBase64Data = false)
        => _client.CallTool<SwImageExportResult>("ExportCurrentViewPng", SolidWorksMcpHubTestClient.Args(
            ("outputPath", outputPath),
            ("width", width),
            ("height", height),
            ("includeBase64Data", includeBase64Data)));

    public SwDocumentInfo[] ListDocuments()
        => _client.CallTool<SwDocumentInfo[]>("ListDocuments");

    public SwDocumentInfo? GetActiveDocument()
        => _client.CallToolNullable<SwDocumentInfo>("GetActiveDocument");
}

internal sealed class McpSelectionService : ISelectionService
{
    private readonly SolidWorksMcpHubTestClient _client;

    public McpSelectionService(SolidWorksMcpHubTestClient client)
    {
        _client = client;
    }

    public SelectionResult SelectByName(string name, string selType)
        => _client.CallTool<SelectionResult>("SelectByName", SolidWorksMcpHubTestClient.Args(
            ("name", name),
            ("selType", selType)));

    public IReadOnlyList<SelectableEntityInfo> ListEntities(SelectableEntityType? entityType = null, string? componentName = null)
        => _client.CallTool<List<SelectableEntityInfo>>("ListEntities", SolidWorksMcpHubTestClient.Args(
            ("entityType", entityType?.ToString()),
            ("componentName", componentName)));

    public IReadOnlyList<ReferencePlaneInfo> ListReferencePlanes()
        => _client.CallTool<List<ReferencePlaneInfo>>("ListReferencePlanes");

    public SolidWorksContextInfo GetSolidWorksContext()
        => _client.CallTool<SolidWorksContextInfo>("GetSolidWorksContext");

    public IReadOnlyList<FeatureTreeItemInfo> ListFeatureTree()
        => _client.CallTool<List<FeatureTreeItemInfo>>("ListFeatureTree");

    public FeatureDiagnosticsResult GetFeatureDiagnostics()
        => _client.CallTool<FeatureDiagnosticsResult>("GetFeatureDiagnostics");

    public EditStateInfo GetEditState()
        => _client.CallTool<EditStateInfo>("GetEditState");

    public SelectionResult SelectEntity(SelectableEntityType entityType, int index, bool append = false, int mark = 0, string? componentName = null)
        => _client.CallTool<SelectionResult>("SelectEntity", SolidWorksMcpHubTestClient.Args(
            ("entityType", entityType.ToString()),
            ("index", index),
            ("append", append),
            ("mark", mark),
            ("componentName", componentName)));

    public EntityMeasurementResult MeasureEntities(
        SelectableEntityType firstEntityType,
        int firstIndex,
        SelectableEntityType secondEntityType,
        int secondIndex,
        string? firstComponentName = null,
        string? secondComponentName = null,
        int arcOption = 1)
        => _client.CallTool<EntityMeasurementResult>("MeasureEntities", SolidWorksMcpHubTestClient.Args(
            ("firstEntityType", firstEntityType.ToString()),
            ("firstIndex", firstIndex),
            ("secondEntityType", secondEntityType.ToString()),
            ("secondIndex", secondIndex),
            ("firstComponentName", firstComponentName),
            ("secondComponentName", secondComponentName),
            ("arcOption", arcOption)));

    public SelectionResult DeleteFeatureByName(string featureName)
        => _client.CallTool<SelectionResult>("DeleteFeatureByName", SolidWorksMcpHubTestClient.Args(("featureName", featureName)));

    public DeleteFeaturesResult DeleteUnusedSketches()
        => _client.CallTool<DeleteFeaturesResult>("DeleteUnusedSketches");

    public void ClearSelection()
        => _client.CallToolText("ClearSelection");
}

internal sealed class McpSketchService : ISketchService
{
    private readonly SolidWorksMcpHubTestClient _client;

    public McpSketchService(SolidWorksMcpHubTestClient client)
    {
        _client = client;
    }

    public void InsertSketch() => _client.CallToolText("InsertSketch");

    public void FinishSketch() => _client.CallToolText("FinishSketch");

    public void SketchUseEdge3(bool chain = false, bool innerLoops = true)
        => _client.CallToolText("SketchUseEdge3", SolidWorksMcpHubTestClient.Args(
            ("chain", chain),
            ("innerLoops", innerLoops)));

    public SketchEntityInfo AddPoint(double x, double y)
        => _client.CallTool<SketchEntityInfo>("AddPoint", SolidWorksMcpHubTestClient.Args(("x", x), ("y", y)));

    public SketchEntityInfo AddEllipse(double cx, double cy, double majorX, double majorY, double minorX, double minorY)
        => _client.CallTool<SketchEntityInfo>("AddEllipse", SolidWorksMcpHubTestClient.Args(
            ("cx", cx),
            ("cy", cy),
            ("majorX", majorX),
            ("majorY", majorY),
            ("minorX", minorX),
            ("minorY", minorY)));

    public SketchEntityInfo AddPolygon(double cx, double cy, double x, double y, int sides, bool inscribed)
        => _client.CallTool<SketchEntityInfo>("AddPolygon", SolidWorksMcpHubTestClient.Args(
            ("cx", cx),
            ("cy", cy),
            ("x", x),
            ("y", y),
            ("sides", sides),
            ("inscribed", inscribed)));

    public SketchEntityInfo AddText(double x, double y, string text, SketchTextOptions? options = null)
        => _client.CallTool<SketchEntityInfo>(
            "AddText",
            SolidWorksMcpHubTestClient.Args(
                ("x", x),
                ("y", y),
                ("text", text),
                ("justification", options?.Justification.ToString()),
                ("flipDirection", options?.FlipDirection),
                ("horizontalMirror", options?.HorizontalMirror),
                ("height", options?.Height),
                ("fontName", options?.FontName),
                ("bold", options?.Bold),
                ("italic", options?.Italic),
                ("underline", options?.Underline),
                ("widthFactor", options?.WidthFactor),
                ("charSpacingFactor", options?.CharSpacingFactor),
                ("rotationDegrees", options?.RotationDegrees)));

    public SketchEntityInfo AddLine(double x1, double y1, double x2, double y2)
        => _client.CallTool<SketchEntityInfo>("AddLine", SolidWorksMcpHubTestClient.Args(
            ("x1", x1),
            ("y1", y1),
            ("x2", x2),
            ("y2", y2)));

    public SketchEntityInfo AddCircle(double cx, double cy, double radius)
        => _client.CallTool<SketchEntityInfo>("AddCircle", SolidWorksMcpHubTestClient.Args(
            ("cx", cx),
            ("cy", cy),
            ("radius", radius)));

    public SketchEntityInfo AddRectangle(double x1, double y1, double x2, double y2)
        => _client.CallTool<SketchEntityInfo>("AddRectangle", SolidWorksMcpHubTestClient.Args(
            ("x1", x1),
            ("y1", y1),
            ("x2", x2),
            ("y2", y2)));

    public SketchEntityInfo AddArc(double cx, double cy, double x1, double y1, double x2, double y2, int direction)
        => _client.CallTool<SketchEntityInfo>("AddArc", SolidWorksMcpHubTestClient.Args(
            ("cx", cx),
            ("cy", cy),
            ("x1", x1),
            ("y1", y1),
            ("x2", x2),
            ("y2", y2),
            ("direction", direction)));
}

internal sealed class McpFeatureService : IFeatureService
{
    private readonly SolidWorksMcpHubTestClient _client;

    public McpFeatureService(SolidWorksMcpHubTestClient client)
    {
        _client = client;
    }

    public FeatureInfo Extrude(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false)
        => _client.CallTool<FeatureInfo>("Extrude", SolidWorksMcpHubTestClient.Args(
            ("depth", depth),
            ("endCondition", (int)endCondition),
            ("flipDirection", flipDirection)));

    public FeatureInfo ExtrudeCut(double depth, EndCondition endCondition = EndCondition.Blind, bool flipDirection = false)
        => _client.CallTool<FeatureInfo>("ExtrudeCut", SolidWorksMcpHubTestClient.Args(
            ("depth", depth),
            ("endCondition", (int)endCondition),
            ("flipDirection", flipDirection)));

    public FeatureInfo Revolve(double angleDegrees, bool isCut = false)
        => _client.CallTool<FeatureInfo>("Revolve", SolidWorksMcpHubTestClient.Args(
            ("angleDegrees", angleDegrees),
            ("isCut", isCut)));

    public FeatureInfo Fillet(double radius)
        => _client.CallTool<FeatureInfo>("Fillet", SolidWorksMcpHubTestClient.Args(("radius", radius)));

    public FeatureInfo Chamfer(double distance)
        => _client.CallTool<FeatureInfo>("Chamfer", SolidWorksMcpHubTestClient.Args(("distance", distance)));

    public FeatureInfo Shell(double thickness)
        => _client.CallTool<FeatureInfo>("Shell", SolidWorksMcpHubTestClient.Args(("thickness", thickness)));
}

internal sealed class McpAssemblyService : IAssemblyService
{
    private readonly SolidWorksMcpHubTestClient _client;

    public McpAssemblyService(SolidWorksMcpHubTestClient client)
    {
        _client = client;
    }

    public ComponentInfo InsertComponent(string filePath, double x = 0, double y = 0, double z = 0)
        => _client.CallTool<ComponentInfo>("InsertComponent", SolidWorksMcpHubTestClient.Args(
            ("filePath", filePath),
            ("x", x),
            ("y", y),
            ("z", z)));

    public MateOperationResult AddMateCoincident(MateAlign align = MateAlign.Closest)
        => _client.CallTool<MateOperationResult>("AddMateCoincident", SolidWorksMcpHubTestClient.Args(("align", (int)align)));

    public MateOperationResult AddMateConcentric(MateAlign align = MateAlign.Closest)
        => _client.CallTool<MateOperationResult>("AddMateConcentric", SolidWorksMcpHubTestClient.Args(("align", (int)align)));

    public MateOperationResult AddMateParallel(MateAlign align = MateAlign.Closest)
        => _client.CallTool<MateOperationResult>("AddMateParallel", SolidWorksMcpHubTestClient.Args(("align", (int)align)));

    public MateOperationResult AddMateDistance(double distance, MateAlign align = MateAlign.Closest)
        => _client.CallTool<MateOperationResult>("AddMateDistance", SolidWorksMcpHubTestClient.Args(
            ("distance", distance),
            ("align", (int)align)));

    public MateOperationResult AddMateAngle(double angleDegrees, MateAlign align = MateAlign.Closest)
        => _client.CallTool<MateOperationResult>("AddMateAngle", SolidWorksMcpHubTestClient.Args(
            ("angleDegrees", angleDegrees),
            ("align", (int)align)));

    public IReadOnlyList<ComponentInfo> ListComponents()
        => _client.CallTool<List<ComponentInfo>>("ListComponents");

    public IReadOnlyList<ComponentInstanceInfo> ListComponentsRecursive()
        => _client.CallTool<List<ComponentInstanceInfo>>("ListComponentsRecursive");

    public AssemblyTargetResolutionResult ResolveComponentTarget(string? componentName = null, string? hierarchyPath = null, string? componentPath = null)
        => _client.CallTool<AssemblyTargetResolutionResult>("ResolveComponentTarget", SolidWorksMcpHubTestClient.Args(
            ("componentName", componentName),
            ("hierarchyPath", hierarchyPath),
            ("componentPath", componentPath)));

    public SharedPartEditImpactResult AnalyzeSharedPartEditImpact(string? componentName = null, string? hierarchyPath = null, string? componentPath = null)
        => _client.CallTool<SharedPartEditImpactResult>("AnalyzeSharedPartEditImpact", SolidWorksMcpHubTestClient.Args(
            ("componentName", componentName),
            ("hierarchyPath", hierarchyPath),
            ("componentPath", componentPath)));

    public AssemblyInterferenceCheckResult CheckInterference(IReadOnlyList<string>? hierarchyPaths = null, bool treatCoincidenceAsInterference = false)
        => _client.CallTool<AssemblyInterferenceCheckResult>("CheckInterference", SolidWorksMcpHubTestClient.Args(
            ("hierarchyPaths", hierarchyPaths?.ToArray()),
            ("treatCoincidenceAsInterference", treatCoincidenceAsInterference)));

    public AssemblyComponentReplacementResult ReplaceComponent(
        string hierarchyPath,
        string replacementFilePath,
        string configName = "",
        bool replaceAllInstances = false,
        int useConfigChoice = 0,
        bool reattachMates = true)
        => _client.CallTool<AssemblyComponentReplacementResult>("ReplaceComponent", SolidWorksMcpHubTestClient.Args(
            ("hierarchyPath", hierarchyPath),
            ("replacementFilePath", replacementFilePath),
            ("configName", configName),
            ("replaceAllInstances", replaceAllInstances),
            ("useConfigChoice", useConfigChoice),
            ("reattachMates", reattachMates)));
}
