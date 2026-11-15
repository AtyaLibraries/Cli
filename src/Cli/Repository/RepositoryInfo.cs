#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Repository DTO has no behavior.")]
internal sealed class RepositoryInfo
{
    internal RepositoryInfo(string root, string name, ProjectInfo? sourceProject, IReadOnlyList<ProjectInfo> projects, IReadOnlyDictionary<string, string> rootFiles)
    {
        Root = root;
        Name = name;
        SourceProject = sourceProject;
        Projects = projects;
        RootFiles = rootFiles;
    }

    internal string Root { get; }

    internal string Name { get; }

    internal ProjectInfo? SourceProject { get; }

    internal IReadOnlyList<ProjectInfo> Projects { get; }

    internal IReadOnlyDictionary<string, string> RootFiles { get; }
}
