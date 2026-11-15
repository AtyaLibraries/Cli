#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Output writers are covered through command-level smoke tests.")]
internal static class OutputWriter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    internal static void Write(CheckResult result, RepositoryInfo repository, CliOptions options)
    {
        if (options.Format == "json")
        {
            WriteJson(result, repository);
            return;
        }

        WriteText(result, options);
    }

    private static void WriteText(CheckResult result, CliOptions options)
    {
        foreach (var group in result.Findings.GroupBy(static f => f.Code.Split('-')[0]).OrderBy(static g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine(group.Key);
            foreach (var finding in group.OrderBy(static f => f.Code, StringComparer.Ordinal))
            {
                var status = finding.Severity switch
                {
                    DiagnosticSeverity.Error => "FAIL",
                    DiagnosticSeverity.Warning => "WARN",
                    _ => "INFO",
                };
                Console.WriteLine($"  {status} {finding.Code}: {finding.Message}");
                if (options.Verbose && finding.File is not null)
                {
                    Console.WriteLine($"    {finding.File}");
                }
            }
        }

        Console.WriteLine(FormattableString.Invariant($"Failed with {result.Errors} errors, {result.Warnings} warnings, {result.Passed} passed, {result.Skipped} skipped."));
    }

    private static void WriteJson(CheckResult result, RepositoryInfo repository)
    {
        var payload = new
        {
            status = result.Errors > 0 ? "failed" : result.Warnings > 0 ? "warning" : "passed",
            workingDirectory = repository.Root,
            constitutionVersion = "1.2.0",
            summary = new
            {
                passed = result.Passed,
                warnings = result.Warnings,
                errors = result.Errors,
                skipped = result.Skipped,
            },
            findings = result.Findings.Select(static f => new
            {
                code = f.Code,
                severity = f.Severity.ToString(),
                message = f.Message,
                file = f.File,
                recommendation = f.Recommendation,
            }),
        };
        Console.WriteLine(JsonSerializer.Serialize(payload, s_jsonOptions));
    }
}
