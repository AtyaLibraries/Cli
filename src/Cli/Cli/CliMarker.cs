#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

/// <summary>
/// Marker type for locating the Atya.Tooling.Cli assembly.
/// </summary>
public static class CliMarker
{
    /// <summary>
    /// Gets the package identifier used by the Atya CLI tool.
    /// </summary>
    public static string PackageId => "Atya.Tooling.Cli";
}
