#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Project DTO has no behavior.")]
internal sealed class ProjectInfo
{
    internal ProjectInfo(
        string path,
        string directory,
        string fileName,
        string kind,
        XDocument? xml,
        IReadOnlyDictionary<string, string> properties,
        IReadOnlyList<string> packageReferences)
    {
        Path = path;
        Directory = directory;
        FileName = fileName;
        Kind = kind;
        Xml = xml;
        Properties = properties;
        PackageReferences = packageReferences;
    }

    internal string Path { get; }

    internal string Directory { get; }

    internal string FileName { get; }

    internal string Kind { get; }

    internal XDocument? Xml { get; }

    internal IReadOnlyDictionary<string, string> Properties { get; }

    internal IReadOnlyList<string> PackageReferences { get; }
}
