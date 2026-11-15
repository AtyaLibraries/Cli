#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Diagnostic DTO has no behavior.")]
internal sealed class DiagnosticFinding
{
    internal DiagnosticFinding(string code, DiagnosticSeverity severity, string message, string? file = null, string? recommendation = null)
    {
        Code = code;
        Severity = severity;
        Message = message;
        File = file;
        Recommendation = recommendation;
    }

    internal string Code { get; }

    internal DiagnosticSeverity Severity { get; }

    internal string Message { get; }

    internal string? File { get; }

    internal string? Recommendation { get; }
}
