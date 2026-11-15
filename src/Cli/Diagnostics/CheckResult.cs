#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Diagnostic summary DTO has no behavior.")]
internal sealed class CheckResult
{
    internal CheckResult(IReadOnlyList<DiagnosticFinding> findings, int passed, int skipped)
    {
        Findings = findings;
        Passed = passed;
        Skipped = skipped;
    }

    internal IReadOnlyList<DiagnosticFinding> Findings { get; }

    internal int Passed { get; }

    internal int Skipped { get; }

    internal int Errors => Findings.Count(static f => f.Severity == DiagnosticSeverity.Error);

    internal int Warnings => Findings.Count(static f => f.Severity == DiagnosticSeverity.Warning);
}
