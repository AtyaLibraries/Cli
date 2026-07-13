#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Constitution constants have no behavior.")]
internal static partial class ConstitutionRules
{
    internal static readonly Regex s_packageIdRegex = PackageIdPattern();

    internal static readonly HashSet<string> s_allowedAreas = new(StringComparer.Ordinal)
    {
        "Foundation", "Errors", "Diagnostics", "Web", "Http", "Hosting", "Messaging",
        "Application", "Data", "Security", "Governance", "Build", "Templates", "Tooling",
    };

    internal static readonly HashSet<string> s_forbiddenFinalSegments = new(StringComparer.Ordinal)
    {
        "Core", "Utilities", "Helpers", "Common", "Shared", "Misc", "Constants",
    };

    internal static readonly string[] s_requiredRootFiles =
    [
        ".editorconfig",
        ".gitattributes",
        ".gitignore",
        "renovate.json",
        "LICENSE",
        "global.json",
        "Directory.Packages.props",
        "README.md",
    ];

    internal static bool IsSdkInjectedPackage(string packageId)
        => packageId.StartsWith("MinVer", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("Microsoft.SourceLink", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("coverlet", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("FluentAssertions", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("NSubstitute", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("Atya.Governance.", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^Atya\.[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)+$")]
    private static partial Regex PackageIdPattern();
}
