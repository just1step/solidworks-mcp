using SolidWorksBridge.SolidWorks;

namespace SolidWorksBridge.Tests.SolidWorks;

public class SolidWorksTemplateLocatorTests : IDisposable
{
    private readonly string _rootDirectory;

    public SolidWorksTemplateLocatorTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"solidworks-template-locator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public void ResolveDefaultTemplatePath_UsesExistingPreferredPath()
    {
        string preferred = CreateFile(@"preferred\custom.prtdot");
        string fallbackDirectory = CreateDirectory("fallback");
        _ = CreateFile(@"fallback\gb_part.prtdot");

        string result = SolidWorksTemplateLocator.ResolveDefaultTemplatePath(
            preferred,
            SwDocType.Part,
            executablePath: @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS 2025\SOLIDWORKS\sldworks.exe",
            candidateDirectories: [fallbackDirectory]);

        Assert.Equal(preferred, result);
    }

    [Fact]
    public void ResolveDefaultTemplatePath_FallsBackToPartTemplateInCandidateDirectory()
    {
        string fallbackDirectory = CreateDirectory("templates");
        string expected = CreateFile(@"templates\gb_part.prtdot");

        string result = SolidWorksTemplateLocator.ResolveDefaultTemplatePath(
            preferredPath: string.Empty,
            docType: SwDocType.Part,
            executablePath: @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS 2025\SOLIDWORKS\sldworks.exe",
            candidateDirectories: [fallbackDirectory]);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveDefaultTemplatePath_FallsBackToAssemblyTemplateInCandidateDirectory()
    {
        string fallbackDirectory = CreateDirectory("templates");
        string expected = CreateFile(@"templates\gb_assembly.asmdot");

        string result = SolidWorksTemplateLocator.ResolveDefaultTemplatePath(
            preferredPath: null,
            docType: SwDocType.Assembly,
            executablePath: @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS 2025\SOLIDWORKS\sldworks.exe",
            candidateDirectories: [fallbackDirectory]);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveDefaultTemplatePath_FallsBackToAnyDrawingTemplateWhenPreferredNamesAreMissing()
    {
        string fallbackDirectory = CreateDirectory("templates");
        string expected = CreateFile(@"templates\custom_sheet.drwdot");

        string result = SolidWorksTemplateLocator.ResolveDefaultTemplatePath(
            preferredPath: null,
            docType: SwDocType.Drawing,
            executablePath: @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS 2025\SOLIDWORKS\sldworks.exe",
            candidateDirectories: [fallbackDirectory]);

        Assert.Equal(expected, result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private string CreateDirectory(string relativePath)
    {
        string fullPath = Path.Combine(_rootDirectory, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private string CreateFile(string relativePath)
    {
        string fullPath = Path.Combine(_rootDirectory, relativePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, "template");
        return fullPath;
    }
}