#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Release verification is an online integration path exercised by command smoke tests.")]
internal sealed partial class ReleaseVerifier
{
    private readonly OnlineChecks _online;

    internal ReleaseVerifier(OnlineChecks online)
    {
        _online = online;
    }

    internal async Task<CheckResult> VerifyAsync(RepositoryInfo repository, string version)
    {
        var builder = new ResultBuilder();
        var normalized = version.TrimStart('v');
        var source = repository.SourceProject!;
        source.Properties.TryGetValue("PackageId", out var packageId);

        var latest = await _online.GetLatestStableVersionAsync(packageId).ConfigureAwait(false);
        builder.Expect(latest == normalized, "VERIFY-001", DiagnosticSeverity.Error, $"nuget.org latest stable must be {normalized}; observed {latest ?? "<none>"}.", source.Path);

        var baseline = await _online.GetDevelopmentBaselineAsync(repository.Name).ConfigureAwait(false);
        builder.Expect(baseline == normalized, "VERIFY-002", DiagnosticSeverity.Error, $"development baseline must be {normalized}; observed {baseline ?? "<none>"}.", source.Path);

        var trees = await _online.GetBranchTreeShasAsync(repository.Name).ConfigureAwait(false);
        builder.Expect(trees.DevelopmentTreeSha == trees.MasterTreeSha, "VERIFY-003", DiagnosticSeverity.Error, "master tree SHA must equal development tree SHA.", repository.Root);

        builder.Expect(await _online.IsMasterCiGreenAsync(repository.Name).ConfigureAwait(false), "VERIFY-004", DiagnosticSeverity.Error, "latest master CI run must have succeeded.", repository.Root);
        return builder.ToResult();
    }
}
