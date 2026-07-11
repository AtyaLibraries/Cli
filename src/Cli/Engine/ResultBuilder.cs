#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Diagnostic accumulator is covered through command-level smoke tests.")]
internal sealed class ResultBuilder
{
    private readonly List<DiagnosticFinding> _findings = [];
    private int _passed;
    private int _skipped;

    internal void Expect(bool condition, string code, DiagnosticSeverity severity, string message, string? file = null, string? recommendation = null)
    {
        if (condition)
        {
            _passed++;
        }
        else
        {
            Fail(code, severity, message, file, recommendation);
        }
    }

    internal void Fail(string code, DiagnosticSeverity severity, string message, string? file = null, string? recommendation = null)
        => _findings.Add(new DiagnosticFinding(code, severity, message, file, recommendation));

    internal void Skip(string code, string message)
    {
        _skipped++;
        _findings.Add(new DiagnosticFinding(code, DiagnosticSeverity.Info, message));
    }

    internal void Add(DiagnosticFinding finding) => _findings.Add(finding);

    internal void AddPassed(int passed) => _passed += passed;

    internal CheckResult ToResult() => new(_findings, _passed, _skipped);
}
