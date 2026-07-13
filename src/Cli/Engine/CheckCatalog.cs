#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Static catalog is validated by meta-tests.")]
internal static class CheckCatalog
{
    private static readonly IReadOnlyList<DiagnosticCodeDescriptor> s_all =
    [
        Doctor(DiagnosticCodes.Repo001, DiagnosticSeverity.Error, "repoStructure"),
        Doctor(DiagnosticCodes.Repo002, DiagnosticSeverity.Error, "repoStructure"),
        Doctor(DiagnosticCodes.Repo003, DiagnosticSeverity.Error, "repoStructure"),
        Doctor(DiagnosticCodes.Repo004, DiagnosticSeverity.Error, "repoStructure"),
        Doctor(DiagnosticCodes.Sdk001, DiagnosticSeverity.Error, "targetFrameworkPolicy"),
        Doctor(DiagnosticCodes.Sdk002, DiagnosticSeverity.Error, "targetFrameworkPolicy"),
        Doctor(DiagnosticCodes.Sdk003, DiagnosticSeverity.Error, "solutionStrategy"),
        Doctor(DiagnosticCodes.Sdk004, DiagnosticSeverity.Error, "solutionStrategy"),
        Doctor(DiagnosticCodes.Name001, DiagnosticSeverity.Error, "naming"),
        Doctor(DiagnosticCodes.Name002, DiagnosticSeverity.Error, "naming"),
        Doctor(DiagnosticCodes.Name003, DiagnosticSeverity.Error, "naming"),
        Doctor(DiagnosticCodes.Name004, DiagnosticSeverity.Error, "naming"),
        Doctor(DiagnosticCodes.Cpm001, DiagnosticSeverity.Error, "dependencyRules"),
        Doctor(DiagnosticCodes.Cpm002, DiagnosticSeverity.Error, "dependencyRules"),
        Doctor(DiagnosticCodes.Cpm003, DiagnosticSeverity.Error, "dependencyRules"),
        Doctor(DiagnosticCodes.Cpm004, DiagnosticSeverity.Error, "dependencyRules"),
        Doctor(DiagnosticCodes.Cpm005, DiagnosticSeverity.Warning, "dependencyRules"),
        Doctor(DiagnosticCodes.Ci001, DiagnosticSeverity.Error, "ciCd"),
        Doctor(DiagnosticCodes.Ci002, DiagnosticSeverity.Error, "ciCd"),
        Doctor(DiagnosticCodes.Ci003, DiagnosticSeverity.Error, "ciCd"),
        Doctor(DiagnosticCodes.Ci004, DiagnosticSeverity.Error, "ciCd"),
        Doctor(DiagnosticCodes.Pkg001, DiagnosticSeverity.Error, "packaging"),
        Doctor(DiagnosticCodes.Pkg002, DiagnosticSeverity.Error, "packaging"),
        Doctor(DiagnosticCodes.Pkg003, DiagnosticSeverity.Error, "packaging"),
        Doctor(DiagnosticCodes.Pkg004, DiagnosticSeverity.Error, "packaging"),
        Doctor(DiagnosticCodes.Pkg005, DiagnosticSeverity.Error, "packaging"),
        Doctor(DiagnosticCodes.Pkg006, DiagnosticSeverity.Warning, "versioningAndReleasePolicy"),
        Doctor(DiagnosticCodes.Pkg007, DiagnosticSeverity.Error, "packaging"),
        Doctor(DiagnosticCodes.Rel001, DiagnosticSeverity.Error, "versioningAndReleasePolicy", requiresOnline: true),
        Doctor(DiagnosticCodes.Rel002, DiagnosticSeverity.Warning, "versioningAndReleasePolicy", requiresOnline: true),
        Doctor(DiagnosticCodes.Rel003, DiagnosticSeverity.Error, "versioningAndReleasePolicy", requiresOnline: true),
        ReleaseCheck(DiagnosticCodes.RelChk001, "versioningAndReleasePolicy"),
        ReleaseCheck(DiagnosticCodes.RelChk002, "versioningAndReleasePolicy"),
        ReleaseCheck(DiagnosticCodes.RelChk003, "versioningAndReleasePolicy"),
        ReleaseCheck(DiagnosticCodes.RelChk004, "versioningAndReleasePolicy"),
        ReleaseCheck(DiagnosticCodes.RelChk005, "ciCd"),
        ReleaseCheck(DiagnosticCodes.RelChk006, "versioningAndReleasePolicy"),
        ReleaseCheck(DiagnosticCodes.RelChk007, DiagnosticSeverity.Warning, "ciCd"),
        ReleaseCheck(DiagnosticCodes.RelChk008, DiagnosticSeverity.Warning, "ciCd"),
        Verify(DiagnosticCodes.Verify001, "versioningAndReleasePolicy"),
        Verify(DiagnosticCodes.Verify002, "versioningAndReleasePolicy"),
        Verify(DiagnosticCodes.Verify003, "versioningAndReleasePolicy"),
        Verify(DiagnosticCodes.Verify004, "ciCd"),
    ];

    internal static IReadOnlyList<DiagnosticCodeDescriptor> All => s_all;

    private static DiagnosticCodeDescriptor Doctor(string code, DiagnosticSeverity severity, string section, bool requiresOnline = false)
        => new(code, severity, section, "doctor", requiresOnline);

    private static DiagnosticCodeDescriptor ReleaseCheck(string code, string section)
        => ReleaseCheck(code, DiagnosticSeverity.Error, section);

    private static DiagnosticCodeDescriptor ReleaseCheck(string code, DiagnosticSeverity severity, string section)
        => new(code, severity, section, "release check", requiresOnline: true);

    private static DiagnosticCodeDescriptor Verify(string code, string section)
        => new(code, DiagnosticSeverity.Error, section, "release verify", requiresOnline: true);
}
