#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Doctor matrix is validated by CLI smoke tests in v1; fixture-per-code tests are planned next.")]
internal sealed partial class DoctorChecker
{
    private readonly OnlineChecks _online;

    internal DoctorChecker(OnlineChecks online)
    {
        _online = online;
    }

    internal async Task<CheckResult> CheckAsync(RepositoryInfo repository, bool online)
    {
        var builder = new ResultBuilder();
        CheckRepository(repository, builder);
        CheckSdk(repository, builder);
        CheckNames(repository, builder);
        CheckCpm(repository, builder);
        CheckCi(repository, builder);
        CheckPackage(repository, builder);
        if (online)
        {
            await CheckReleaseAsync(repository, builder).ConfigureAwait(false);
        }
        else
        {
            builder.Skip("REL-001", "REL checks skipped because --online was not passed.");
            builder.Skip("REL-002", "REL checks skipped because --online was not passed.");
            builder.Skip("REL-003", "REL checks skipped because --online was not passed.");
        }

        return builder.ToResult();
    }

    private static void CheckRepository(RepositoryInfo repository, ResultBuilder builder)
    {
        var forbidden = new[]
        {
            "Directory.Build.props",
            "Directory.Build.targets",
            "nuget.config",
            ".github/dependabot.yml",
        };
        foreach (var relative in forbidden)
        {
            var path = Combine(repository.Root, relative);
            builder.Expect(!File.Exists(path), "REPO-001", DiagnosticSeverity.Error, $"Forbidden file present: {relative}", path);
        }

        foreach (var directory in Directory.EnumerateDirectories(repository.Root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(directory);
            if (name is "artifacts" or "BenchmarkDotNet.Artifacts" or "TestResults")
            {
                var relative = Path.GetRelativePath(repository.Root, directory);
                if (HasTrackedContent(repository.Root, relative))
                {
                    builder.Fail("REPO-001", DiagnosticSeverity.Error, $"Forbidden committed output directory present: {relative}", directory);
                }
            }
        }

        var sourceName = repository.SourceProject is null
            ? repository.Name
            : Path.GetFileNameWithoutExtension(repository.SourceProject.FileName);
        var requiredLayout = new[]
        {
            $"{repository.Name}.sln",
            $"src/{sourceName}",
            $"tests/{sourceName}.UnitTests",
            $"benchmarks/{sourceName}.Benchmarks",
            $"samples/{sourceName}.Samples.Console",
            "tests/Directory.Build.props",
        };
        foreach (var relative in requiredLayout)
        {
            var path = Combine(repository.Root, relative);
            builder.Expect(File.Exists(path) || Directory.Exists(path), "REPO-002", DiagnosticSeverity.Error, $"Required layout missing: {relative}", path);
        }

        foreach (var relative in ConstitutionRules.s_requiredRootFiles)
        {
            var path = Combine(repository.Root, relative);
            builder.Expect(File.Exists(path), "REPO-003", DiagnosticSeverity.Error, $"Required root file missing: {relative}", path);
        }

        var packedReadme = $"src/{sourceName}/README.md";
        builder.Expect(File.Exists(Combine(repository.Root, packedReadme)), "REPO-004", DiagnosticSeverity.Error, "Packed NuGet README missing.", Combine(repository.Root, packedReadme));
    }

    private static void CheckSdk(RepositoryInfo repository, ResultBuilder builder)
    {
        var globalJson = Combine(repository.Root, "global.json");
        if (TryReadJson(globalJson, out var json))
        {
            var sdkVersion = json.RootElement.GetPropertyOrNull("sdk")?.GetPropertyOrNull("version")?.GetString();
            var rollForward = json.RootElement.GetPropertyOrNull("sdk")?.GetPropertyOrNull("rollForward")?.GetString();
            var allowPrerelease = json.RootElement.GetPropertyOrNull("sdk")?.GetPropertyOrNull("allowPrerelease")?.GetBoolean();
            var buildSdk = json.RootElement.GetPropertyOrNull("msbuild-sdks")?.GetPropertyOrNull("Atya.Build.Sdk")?.GetString();

            builder.Expect(IsSdkVersion(sdkVersion), "SDK-002", DiagnosticSeverity.Error, "global.json sdk.version must be 10.0.3xx.", globalJson);
            builder.Expect(rollForward == "latestPatch" && allowPrerelease == false, "SDK-002", DiagnosticSeverity.Error, "global.json must use rollForward latestPatch and allowPrerelease false.", globalJson);
            builder.Expect(IsAtLeast(buildSdk, new Version(1, 5, 2)), "SDK-001", DiagnosticSeverity.Error, "global.json must pin Atya.Build.Sdk >= 1.5.2.", globalJson);
        }
        else
        {
            builder.Fail("SDK-001", DiagnosticSeverity.Error, "global.json missing or invalid.", globalJson);
        }

        var source = repository.SourceProject;
        if (source?.Xml is null)
        {
            builder.Fail("SDK-003", DiagnosticSeverity.Error, "Source project missing or invalid.", source?.Path);
            return;
        }

        var projectElement = source.Xml.Root;
        var sdk = projectElement?.Attribute("Sdk")?.Value;
        builder.Expect(sdk == "Atya.Build.Sdk", "SDK-003", DiagnosticSeverity.Error, "Library project must use Sdk=\"Atya.Build.Sdk\".", source.Path);
        builder.Expect(!source.Xml.Descendants().Any(static e => e.Name.LocalName == "Sdk" && e.Attribute("Version") is not null), "SDK-003", DiagnosticSeverity.Error, "Inline SDK versions are forbidden.", source.Path);
        builder.Expect(!source.Properties.ContainsKey("Version"), "SDK-004", DiagnosticSeverity.Error, "Library project must not hardcode <Version>.", source.Path);
        builder.Expect(!source.PackageReferences.Any(ConstitutionRules.IsSdkInjectedPackage), "SDK-004", DiagnosticSeverity.Error, "Library project pins an SDK-injected package.", source.Path);
    }

    private static void CheckNames(RepositoryInfo repository, ResultBuilder builder)
    {
        var source = repository.SourceProject;
        if (source is null)
        {
            return;
        }

        source.Properties.TryGetValue("PackageId", out var packageId);
        source.Properties.TryGetValue("AssemblyName", out var assemblyName);
        source.Properties.TryGetValue("RootNamespace", out var rootNamespace);
        builder.Expect(!string.IsNullOrWhiteSpace(packageId) && ConstitutionRules.s_packageIdRegex.IsMatch(packageId), "NAME-001", DiagnosticSeverity.Error, "PackageId does not match the Atya naming regex.", source.Path);
        builder.Expect(packageId == assemblyName && packageId == rootNamespace, "NAME-002", DiagnosticSeverity.Error, "AssemblyName and RootNamespace must equal PackageId.", source.Path);

        var area = packageId?.Split('.').Skip(1).FirstOrDefault();
        builder.Expect(area is not null && ConstitutionRules.s_allowedAreas.Contains(area), "NAME-003", DiagnosticSeverity.Error, "Area segment is not allowed by the constitution.", source.Path);
        builder.Expect(packageId is null || !ConstitutionRules.s_forbiddenFinalSegments.Contains(packageId.Split('.').Last()), "NAME-004", DiagnosticSeverity.Error, "PackageId uses a forbidden final segment.", source.Path);
    }

    private static void CheckCpm(RepositoryInfo repository, ResultBuilder builder)
    {
        var propsPath = Combine(repository.Root, "Directory.Packages.props");
        if (!TryReadXml(propsPath, out var xml))
        {
            builder.Fail("CPM-001", DiagnosticSeverity.Error, "Directory.Packages.props missing or invalid.", propsPath);
            return;
        }

        var props = xml.Descendants().Where(static e => !e.HasElements && !string.IsNullOrWhiteSpace(e.Value))
            .ToDictionary(static e => e.Name.LocalName, static e => e.Value.Trim(), StringComparer.OrdinalIgnoreCase);
        builder.Expect(props.TryGetValue("ManagePackageVersionsCentrally", out var manage) && manage == "true" &&
            props.TryGetValue("CentralPackageTransitivePinningEnabled", out var transitive) && transitive == "true",
            "CPM-001", DiagnosticSeverity.Error, "Central package management must be enabled with transitive pinning.", propsPath);

        var versions = xml.Descendants()
            .Where(static e => e.Name.LocalName == "PackageVersion")
            .Select(static e => new PackageVersionPin(e.Attribute("Include")?.Value ?? string.Empty, e.Attribute("Version")?.Value ?? string.Empty))
            .Where(static p => !string.IsNullOrWhiteSpace(p.Id))
            .ToArray();
        foreach (var pin in versions.Where(static p => ConstitutionRules.IsSdkInjectedPackage(p.Id)))
        {
            builder.Fail("CPM-002", DiagnosticSeverity.Error, $"SDK-injected package pinned: {pin.Id}", propsPath);
        }

        foreach (var project in repository.Projects)
        {
            builder.Expect(File.Exists(Path.Combine(project.Directory, "packages.lock.json")), "CPM-003", DiagnosticSeverity.Error, $"Missing packages.lock.json for {project.FileName}.", project.Path);
        }

        foreach (var pin in versions.Where(static p => p.Id.StartsWith("Atya.", StringComparison.Ordinal) && p.Version.Contains('-', StringComparison.Ordinal)))
        {
            builder.Fail("CPM-004", DiagnosticSeverity.Error, $"Prerelease runtime dependency pinned: {pin.Id} {pin.Version}", propsPath);
        }

        var referenced = repository.Projects.SelectMany(static p => p.PackageReferences).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in versions.Where(pin => !ConstitutionRules.IsSdkInjectedPackage(pin.Id) && pin.Id != "BenchmarkDotNet" && !referenced.Contains(pin.Id)))
        {
            builder.Fail("CPM-005", DiagnosticSeverity.Warning, $"Pinned package is not referenced by any project: {pin.Id}", propsPath);
        }
    }

    private static void CheckCi(RepositoryInfo repository, ResultBuilder builder)
    {
        var workflows = new[] { "ci.yml", "codeql.yml", "dependency-review.yml", "publish-nuget.yml" };
        foreach (var workflow in workflows)
        {
            builder.Expect(File.Exists(Combine(repository.Root, $".github/workflows/{workflow}")), "CI-001", DiagnosticSeverity.Error, $"Required workflow missing: {workflow}", Combine(repository.Root, $".github/workflows/{workflow}"));
        }

        var ciPath = Combine(repository.Root, ".github/workflows/ci.yml");
        var ci = File.Exists(ciPath) ? File.ReadAllText(ciPath, Encoding.UTF8) : string.Empty;
        builder.Expect(Regex.IsMatch(ci, @"uses:\s*AtyaLibraries/github-workflows/\.github/workflows/dotnet-package-ci\.yml@[0-9a-f]{40}", RegexOptions.IgnoreCase), "CI-002", DiagnosticSeverity.Error, "ci.yml must call the shared workflow pinned by commit SHA.", ciPath);
        builder.Expect(ci.Contains("fail-on-coverage: true", StringComparison.Ordinal) &&
            Regex.IsMatch(ci, @"coverage-line-min:\s*(9[0-9]|100)") &&
            Regex.IsMatch(ci, @"coverage-branch-min:\s*(8[0-9]|9[0-9]|100)") &&
            !ci.Contains("allow-empty-coverage: true", StringComparison.Ordinal),
            "CI-003", DiagnosticSeverity.Error, "Coverage gate must be enabled at 90 line / 80 branch minimums.", ciPath);

        foreach (var path in Directory.Exists(Combine(repository.Root, ".github/workflows"))
            ? Directory.EnumerateFiles(Combine(repository.Root, ".github/workflows"), "*.yml")
            : Enumerable.Empty<string>())
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            builder.Expect(!text.Contains("NUGET_API_KEY", StringComparison.OrdinalIgnoreCase) &&
                !text.Contains("dotnet nuget push", StringComparison.OrdinalIgnoreCase),
                "CI-004", DiagnosticSeverity.Error, "Workflow contains forbidden direct publish path.", path);
        }
    }

    private static void CheckPackage(RepositoryInfo repository, ResultBuilder builder)
    {
        var source = repository.SourceProject;
        if (source is null)
        {
            return;
        }

        var expectedRepository = $"https://github.com/AtyaLibraries/{repository.Name}";
        builder.Expect(source.Properties.ContainsKey("PackageId"), "PKG-001", DiagnosticSeverity.Error, "PackageId missing.", source.Path);
        builder.Expect(source.Properties.TryGetValue("Authors", out var authors) && authors == "Arsen Asulyan", "PKG-002", DiagnosticSeverity.Error, "Authors must be Arsen Asulyan.", source.Path);
        builder.Expect(source.Properties.TryGetValue("Description", out var description) && !string.IsNullOrWhiteSpace(description) && !description.Contains("__", StringComparison.Ordinal), "PKG-003", DiagnosticSeverity.Error, "Description missing or placeholder.", source.Path);
        builder.Expect(source.Properties.TryGetValue("RepositoryUrl", out var repositoryUrl) && repositoryUrl == expectedRepository, "PKG-004", DiagnosticSeverity.Error, $"RepositoryUrl must be {expectedRepository}.", source.Path);
        builder.Expect(!source.Properties.TryGetValue("PackageLicenseExpression", out var license) || license == "MIT", "PKG-005", DiagnosticSeverity.Error, "Explicit PackageLicenseExpression must be MIT.", source.Path);
        builder.Expect(source.Properties.ContainsKey("PackageValidationBaselineVersion"), "PKG-006", DiagnosticSeverity.Warning, "PackageValidationBaselineVersion is required after the first stable publish; first release has no valid baseline.", source.Path);
        builder.Expect(!source.Properties.ContainsKey("PackageIcon") && !Directory.EnumerateFiles(Combine(repository.Root, "src"), "icon.png", SearchOption.AllDirectories).Any(), "PKG-007", DiagnosticSeverity.Error, "Per-repo package icons are forbidden.", source.Path);
    }

    private async Task CheckReleaseAsync(RepositoryInfo repository, ResultBuilder builder)
    {
        var source = repository.SourceProject;
        if (source is null)
        {
            return;
        }

        source.Properties.TryGetValue("PackageId", out var packageId);
        source.Properties.TryGetValue("PackageValidationBaselineVersion", out var baseline);
        if (!string.IsNullOrWhiteSpace(packageId))
        {
            var latest = await _online.GetLatestStableVersionAsync(packageId).ConfigureAwait(false);
            builder.Expect(string.IsNullOrWhiteSpace(latest) || baseline == latest, "REL-001", DiagnosticSeverity.Error, $"PackageValidationBaselineVersion must equal latest published stable ({latest}).", source.Path);

            var prereleaseDependency = await _online.HasPrereleaseDependencyAsync(packageId, latest).ConfigureAwait(false);
            builder.Expect(!prereleaseDependency, "REL-003", DiagnosticSeverity.Error, "Latest published stable depends on a prerelease package.", source.Path);
        }

        var trees = await _online.GetBranchTreeShasAsync(repository.Name).ConfigureAwait(false);
        builder.Expect(trees.DevelopmentTreeSha == trees.MasterTreeSha, "REL-002", DiagnosticSeverity.Warning, "master tree SHA differs from development tree SHA.", repository.Root);
    }

}
