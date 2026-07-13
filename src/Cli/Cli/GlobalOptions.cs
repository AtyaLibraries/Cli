#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Parsed option DTO has no behavior.")]
internal sealed class CliOptions
{
    internal CliOptions(
        string format,
        string workingDirectory,
        bool verbose,
        bool noColor,
        bool ci,
        bool warningsAsErrors,
        bool online,
        string? version,
        bool allowMajor)
    {
        Format = format;
        WorkingDirectory = workingDirectory;
        Verbose = verbose;
        NoColor = noColor;
        Ci = ci;
        WarningsAsErrors = warningsAsErrors;
        Online = online;
        Version = version;
        AllowMajor = allowMajor;
    }

    internal string Format { get; }

    internal string WorkingDirectory { get; }

    internal bool Verbose { get; }

    internal bool NoColor { get; }

    internal bool Ci { get; }

    internal bool WarningsAsErrors { get; }

    internal bool Online { get; }

    internal string? Version { get; }

    internal bool AllowMajor { get; }
}
