using System.Text.RegularExpressions;

namespace SolidWorksBridge.SolidWorks;

internal static class SolidWorksTemplateLocator
{
    private static readonly string[] PartTemplateNames =
    [
        "part.prtdot",
        "gb_part.prtdot",
    ];

    private static readonly string[] AssemblyTemplateNames =
    [
        "assembly.asmdot",
        "gb_assembly.asmdot",
    ];

    private static readonly string[] DrawingTemplateNames =
    [
        "drawing.drwdot",
        "gb_a4.drwdot",
        "a4.drwdot",
    ];

    public static string ResolveDefaultTemplatePath(
        string? preferredPath,
        SwDocType docType,
        string? executablePath,
        IEnumerable<string>? candidateDirectories = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
        {
            return preferredPath;
        }

        foreach (string directory in candidateDirectories ?? GetCandidateDirectories(executablePath))
        {
            string? templatePath = FindTemplateInDirectory(directory, docType);
            if (!string.IsNullOrWhiteSpace(templatePath))
            {
                return templatePath;
            }
        }

        return preferredPath ?? string.Empty;
    }

    internal static IReadOnlyList<string> GetCandidateDirectories(string? executablePath)
    {
        var directories = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int? marketingYear = TryExtractMarketingYear(executablePath);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string publicDocuments = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);

        foreach (string path in BuildYearSpecificDirectories(marketingYear))
        {
            AddIfExists(path, directories, seen);
        }

        foreach (string path in DiscoverInstalledVersionDirectories(programData, "SOLIDWORKS"))
        {
            AddIfExists(path, directories, seen);
        }

        foreach (string path in DiscoverInstalledVersionDirectories(programData, "SolidWorks"))
        {
            AddIfExists(path, directories, seen);
        }

        foreach (string path in DiscoverInstalledVersionDirectories(publicDocuments, "SOLIDWORKS"))
        {
            AddIfExists(path, directories, seen);
        }

        foreach (string path in DiscoverInstalledVersionDirectories(publicDocuments, "SolidWorks"))
        {
            AddIfExists(path, directories, seen);
        }

        foreach (string path in BuildGenericDirectories())
        {
            AddIfExists(path, directories, seen);
        }

        return directories;
    }

    private static IEnumerable<string> BuildYearSpecificDirectories(int? marketingYear)
    {
        if (!marketingYear.HasValue)
        {
            yield break;
        }

        string yearSegment = $"SOLIDWORKS {marketingYear.Value}";
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string publicDocuments = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);

        yield return Path.Combine(programData, "SOLIDWORKS", yearSegment, "templates");
        yield return Path.Combine(programData, "SolidWorks", yearSegment, "templates");
        yield return Path.Combine(publicDocuments, "SOLIDWORKS", yearSegment, "templates");
        yield return Path.Combine(publicDocuments, "SolidWorks", yearSegment, "templates");
    }

    private static IEnumerable<string> BuildGenericDirectories()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string publicDocuments = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);

        yield return Path.Combine(programData, "SOLIDWORKS", "templates");
        yield return Path.Combine(programData, "SolidWorks", "templates");
        yield return Path.Combine(publicDocuments, "SOLIDWORKS", "templates");
        yield return Path.Combine(publicDocuments, "SolidWorks", "templates");
    }

    private static IEnumerable<string> DiscoverInstalledVersionDirectories(string root, string vendorFolderName)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        string vendorRoot = Path.Combine(root, vendorFolderName);
        if (!Directory.Exists(vendorRoot))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateDirectories(vendorRoot, "SOLIDWORKS *", SearchOption.TopDirectoryOnly)
            .Select(path => new
            {
                Path = Path.Combine(path, "templates"),
                Year = TryExtractMarketingYear(path) ?? int.MinValue,
            })
            .OrderByDescending(entry => entry.Year)
            .Select(entry => entry.Path)
            .ToArray();
    }

    private static string? FindTemplateInDirectory(string directory, SwDocType docType)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        foreach (string fileName in GetPreferredTemplateNames(docType))
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (docType == SwDocType.Drawing)
        {
            return Directory
                .EnumerateFiles(directory, "*.drwdot", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        return null;
    }

    private static IEnumerable<string> GetPreferredTemplateNames(SwDocType docType)
    {
        return docType switch
        {
            SwDocType.Part => PartTemplateNames,
            SwDocType.Assembly => AssemblyTemplateNames,
            SwDocType.Drawing => DrawingTemplateNames,
            _ => Array.Empty<string>(),
        };
    }

    private static int? TryExtractMarketingYear(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        Match match = Regex.Match(executablePath, @"SOLIDWORKS\s+(?<year>20\d{2})", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups["year"].Value, out int year)
            ? year
            : null;
    }

    private static void AddIfExists(string path, ICollection<string> directories, ISet<string> seen)
    {
        if (Directory.Exists(path) && seen.Add(path))
        {
            directories.Add(path);
        }
    }
}