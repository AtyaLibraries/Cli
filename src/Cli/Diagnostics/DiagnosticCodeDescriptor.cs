#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Diagnostic catalog DTO has no behavior.")]
internal sealed class DiagnosticCodeDescriptor
{
    internal DiagnosticCodeDescriptor(string code, DiagnosticSeverity severity, string constitutionSection, string pipeline, bool requiresOnline)
    {
        Code = code;
        Severity = severity;
        ConstitutionSection = constitutionSection;
        Pipeline = pipeline;
        RequiresOnline = requiresOnline;
    }

    internal string Code { get; }

    internal DiagnosticSeverity Severity { get; }

    internal string ConstitutionSection { get; }

    internal string Pipeline { get; }

    internal bool RequiresOnline { get; }
}
