#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Repository scanning is covered by command-level smoke tests in v1.")]
internal sealed class RepositoryScanner
{
    internal RepositoryInfo Scan(string root)
    {
        var projects = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => LoadProject(root, path))
            .OrderBy(static p => p.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var source = projects.FirstOrDefault(static p => p.Kind == "Source");
        var rootFiles = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Select(static path => new { Name = Path.GetFileName(path), Path = path })
            .Where(static file => file.Name is not null)
            .ToDictionary(static file => file.Name!, static file => file.Path, StringComparer.OrdinalIgnoreCase);
        return new RepositoryInfo(root, new DirectoryInfo(root).Name, source, projects, rootFiles);
    }

    private static ProjectInfo LoadProject(string root, string path)
    {
        XDocument? xml = null;
        try
        {
            xml = XDocument.Load(path, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (InvalidOperationException)
        {
        }

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var packageReferences = new List<string>();
        if (xml is not null)
        {
            foreach (var element in xml.Descendants().Where(static e => e.Name.LocalName is not "Project" and not "PropertyGroup" and not "ItemGroup"))
            {
                if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
                {
                    properties[element.Name.LocalName] = element.Value.Trim();
                }
            }

            packageReferences.AddRange(xml.Descendants()
                .Where(static e => e.Name.LocalName == "PackageReference")
                .Select(static e => e.Attribute("Include")?.Value)
                .Where(static include => !string.IsNullOrWhiteSpace(include))!);
        }

        var relative = Path.GetRelativePath(root, path);
        var directory = Path.GetDirectoryName(path) ?? root;
        var normalized = relative.Replace(Path.DirectorySeparatorChar, '/');
        var kind = normalized.StartsWith("src/", StringComparison.OrdinalIgnoreCase)
            ? "Source"
            : normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)
                ? "Test"
                : normalized.StartsWith("benchmarks/", StringComparison.OrdinalIgnoreCase)
                    ? "Benchmark"
                    : normalized.StartsWith("samples/", StringComparison.OrdinalIgnoreCase)
                        ? "Sample"
                        : "Unknown";

        return new ProjectInfo(path, directory, Path.GetFileName(path), kind, xml, properties, packageReferences);
    }
}
