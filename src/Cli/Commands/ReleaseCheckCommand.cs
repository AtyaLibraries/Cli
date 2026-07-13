#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Release checks are online integration paths exercised by command smoke tests.")]
internal sealed partial class ReleaseChecker
{
    private readonly OnlineChecks _online;
    private readonly DoctorChecker _doctor;

    internal ReleaseChecker(OnlineChecks online, DoctorChecker doctor)
    {
        _online = online;
        _doctor = doctor;
    }

    internal async Task<CheckResult> CheckAsync(RepositoryInfo repository, string version, bool allowMajor)
    {
        var builder = new ResultBuilder();
        var normalized = version.TrimStart('v');
        var source = repository.SourceProject!;
        source.Properties.TryGetValue("PackageId", out var packageId);

        builder.Expect(SemVerRegex().IsMatch(normalized), "RELCHK-001", DiagnosticSeverity.Error, "Proposed version must be SemVer 2.0.", source.Path);
        var latest = await _online.GetLatestStableVersionAsync(packageId).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(latest) && Version.TryParse(latest, out var latestVersion) && Version.TryParse(normalized, out var nextVersion))
        {
            builder.Expect(allowMajor || nextVersion.Major == latestVersion.Major, "RELCHK-002", DiagnosticSeverity.Error, "Major releases require --allow-major.", source.Path);
            builder.Expect(nextVersion > latestVersion, "RELCHK-002", DiagnosticSeverity.Error, "Proposed version must be greater than latest stable.", source.Path);
        }

        builder.Expect(!await _online.TagExistsAsync(repository.Name, $"v{normalized}").ConfigureAwait(false), "RELCHK-003", DiagnosticSeverity.Error, $"Tag v{normalized} already exists.", repository.Root);
        builder.Expect(!await _online.NuGetVersionExistsAsync(packageId, normalized).ConfigureAwait(false), "RELCHK-004", DiagnosticSeverity.Error, $"{packageId} {normalized} already exists on nuget.org.", source.Path);
        builder.Expect(await _online.IsDevelopmentCiGreenAsync(repository.Name).ConfigureAwait(false), "RELCHK-005", DiagnosticSeverity.Error, "development CI is not green.", repository.Root);
        builder.Expect(!await _online.IsRepositoryPrivateAsync(repository.Name).ConfigureAwait(false), "RELCHK-007", DiagnosticSeverity.Warning, "Repository is private; package repos must be public so the central publisher can check out release tags.", repository.Root);
        builder.Expect(await _online.HasPublisherDispatchSecretAccessAsync(repository.Name).ConfigureAwait(false), "RELCHK-008", DiagnosticSeverity.Warning, "Repository is not in PUBLISHER_DISPATCH_TOKEN selected repositories; tag publish dispatch may fail.", repository.Root);

        foreach (var dependency in await _online.FindStaleAtyaDependenciesAsync(repository).ConfigureAwait(false))
        {
            builder.Fail("RELCHK-006", DiagnosticSeverity.Error, $"Internal dependency is not at latest stable: {dependency}", source.Path);
        }

        var doctor = await _doctor.CheckAsync(repository, online: true).ConfigureAwait(false);
        foreach (var finding in doctor.Findings)
        {
            builder.Add(finding);
        }

        builder.AddPassed(doctor.Passed);
        return builder.ToResult();
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+([+-][0-9A-Za-z.-]+)?$")]
    private static partial Regex SemVerRegex();
}
