using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

internal sealed class SolidWorksIntegrationTestContext : IDisposable
{
    private readonly SolidWorksMcpHubTestClient _hubClient;
    private readonly Dictionary<(string Path, string Title, int Type), int> _baselineDocuments;

    public IDocumentService Documents { get; }
    public ISelectionService Selection { get; }
    public ISketchService Sketch { get; }
    public IFeatureService Feature { get; }
    public IAssemblyService Assembly { get; }
    public ISwConnectionManager ConnectionManager { get; }

    public SolidWorksIntegrationTestContext()
    {
        _hubClient = SolidWorksMcpHubTestClient.Create();

        ConnectionManager = new McpSwConnectionManager(_hubClient);
        Documents = new McpDocumentService(_hubClient);
        Selection = new McpSelectionService(_hubClient);
        Sketch = new McpSketchService(_hubClient);
        Feature = new McpFeatureService(_hubClient);
        Assembly = new McpAssemblyService(_hubClient);

        ConnectionManager.Connect();
        _baselineDocuments = CaptureDocumentCounts(Documents.ListDocuments());
    }

    public void Dispose()
    {
        CleanupCreatedDocuments();
        _hubClient.Dispose();
    }

    public void CleanupCreatedDocuments()
    {
        var remainingBaseline = new Dictionary<(string Path, string Title, int Type), int>(_baselineDocuments);
        var currentDocuments = Documents.ListDocuments();

        for (int index = currentDocuments.Length - 1; index >= 0; index--)
        {
            var document = currentDocuments[index];
            var key = (document.Path ?? string.Empty, document.Title, document.Type);
            if (remainingBaseline.TryGetValue(key, out int count) && count > 0)
            {
                remainingBaseline[key] = count - 1;
                continue;
            }

            try
            {
                Documents.CloseDocument(string.IsNullOrWhiteSpace(document.Path) ? document.Title : document.Path);
            }
            catch
            {
                // Best-effort cleanup against a real shared SolidWorks hub session.
            }
        }
    }

    private static Dictionary<(string Path, string Title, int Type), int> CaptureDocumentCounts(IEnumerable<SwDocumentInfo> documents)
    {
        var counts = new Dictionary<(string Path, string Title, int Type), int>();
        foreach (var document in documents)
        {
            var key = (document.Path ?? string.Empty, document.Title, document.Type);
            counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        return counts;
    }

    public string CreateAndSaveBoxPart(double width = 0.01, double height = 0.01, double depth = 0.005)
    {
        Documents.NewDocument(SwDocType.Part);

        var frontPlane = Selection.ListReferencePlanes()
            .OrderBy(plane => plane.Index)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No reference plane was available for the test part.");

        var selected = Selection.SelectByName(frontPlane.SelectionName, frontPlane.SelectionType);
        if (!selected.Success)
        {
            throw new InvalidOperationException(selected.Message);
        }

        Sketch.InsertSketch();
        Sketch.AddRectangle(-width, -height, width, height);
        Feature.Extrude(depth);

        string path = Path.Combine(Path.GetTempPath(), $"SwTestPart_{Guid.NewGuid():N}.sldprt");
        var save = Documents.SaveDocumentAs(path, sourcePath: null, saveAsCopy: false);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Failed to save the test part to '{path}'.");
        }

        return save.OutputPath;
    }

    public string CreateAndSavePlaneAlignedBlockPart(double width = 0.01, double height = 0.01, double depth = 0.005)
    {
        return CreateAndSaveBoxPart(width, height, depth);
    }

    public string SaveActiveDocumentAs(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("extension must not be empty", nameof(extension));

        string normalizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : $".{extension}";

        var activeDocument = Documents.GetActiveDocument()
            ?? throw new InvalidOperationException("No active document to save.");

        string path = Path.Combine(Path.GetTempPath(), $"SwTestDoc_{Guid.NewGuid():N}{normalizedExtension}");
        var save = Documents.SaveDocumentAs(path, activeDocument.Path, saveAsCopy: false);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Failed to save active document to '{path}'.");
        }

        return save.OutputPath;
    }
}
