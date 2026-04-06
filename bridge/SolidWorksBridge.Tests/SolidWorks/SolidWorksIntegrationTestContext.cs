using SolidWorks.Interop.sldworks;
using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

internal sealed class SolidWorksIntegrationTestContext : IDisposable
{
    private readonly SwConnectionManager _manager;
    private readonly Dictionary<(string Path, string Title, int Type), int> _baselineDocuments;

    public DocumentService Documents { get; }
    public SelectionService Selection { get; }
    public SketchService Sketch { get; }
    public FeatureService Feature { get; }
    public AssemblyService Assembly { get; }
    public ISldWorksApp App => _manager.SwApp!;

    public SolidWorksIntegrationTestContext()
    {
        _manager = new SwConnectionManager(new SwComConnector());
        _manager.Connect();

        Documents = new DocumentService(_manager);
        Selection = new SelectionService(_manager);
        Sketch = new SketchService(_manager);
        Feature = new FeatureService(_manager);
        Assembly = new AssemblyService(_manager);

        _baselineDocuments = CaptureDocumentCounts(Documents.ListDocuments());
    }

    public void Dispose()
    {
        CleanupCreatedDocuments();
        _manager.Disconnect();
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
                // Best-effort cleanup for real SolidWorks integration tests.
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
        Selection.SelectByName("前视基准面", "PLANE");
        Sketch.InsertSketch();
        Sketch.AddRectangle(-width, -height, width, height);
        Feature.Extrude(depth);

        string path = Path.Combine(Path.GetTempPath(), $"SwTestPart_{Guid.NewGuid():N}.sldprt");
        var model = (IModelDoc2)App.IActiveDoc2!;
        model.SaveAs3(path, 0, 0);
        return path;
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

        var model = (IModelDoc2?)App.IActiveDoc2
            ?? throw new InvalidOperationException("No active document to save.");

        string path = Path.Combine(Path.GetTempPath(), $"SwTestDoc_{Guid.NewGuid():N}{normalizedExtension}");
        _ = model.SaveAs3(path, 0, 0);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Failed to save active document to '{path}'.");
        }

        return path;
    }
}