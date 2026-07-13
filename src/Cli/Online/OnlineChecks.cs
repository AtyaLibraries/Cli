#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Online checks wrap NuGet and gh process integration.")]
internal sealed partial class OnlineChecks
{
    private static readonly HttpClient s_http = new();

    internal async Task<string?> GetLatestStableVersionAsync(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return null;
        }

        var versions = await GetVersionsAsync(packageId).ConfigureAwait(false);
        return versions.Where(static v => !v.Contains('-', StringComparison.Ordinal)).LastOrDefault();
    }

    internal async Task<bool> NuGetVersionExistsAsync(string? packageId, string version)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return false;
        }

        var versions = await GetVersionsAsync(packageId).ConfigureAwait(false);
        return versions.Contains(version, StringComparer.OrdinalIgnoreCase);
    }

    internal async Task<bool> HasPrereleaseDependencyAsync(string? packageId, string? version)
    {
        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var nuspec = await s_http.GetStringAsync($"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{version.ToLowerInvariant()}/{packageId.ToLowerInvariant()}.nuspec").ConfigureAwait(false);
        return Regex.IsMatch(nuspec, @"<dependency\s+id=""[^""]+""\s+version=""[^""]+-[^""]*""", RegexOptions.IgnoreCase);
    }

    internal async Task<bool> TagExistsAsync(string repo, string tag)
    {
        var result = await GhAsync("api", $"repos/AtyaLibraries/{repo}/git/ref/tags/{tag}").ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    internal async Task<BranchTrees> GetBranchTreeShasAsync(string repo)
    {
        var development = await GetBranchTreeShaAsync(repo, "development").ConfigureAwait(false);
        var master = await GetBranchTreeShaAsync(repo, "master").ConfigureAwait(false);
        return new BranchTrees(development, master);
    }

    internal async Task<bool> IsDevelopmentCiGreenAsync(string repo) => await IsBranchCiGreenAsync(repo, "development").ConfigureAwait(false);

    internal async Task<bool> IsMasterCiGreenAsync(string repo) => await IsBranchCiGreenAsync(repo, "master").ConfigureAwait(false);

    internal async Task<bool> IsRepositoryPrivateAsync(string repo)
    {
        var metadata = await GhJsonAsync($"repos/AtyaLibraries/{repo}").ConfigureAwait(false);
        return metadata.RootElement.GetProperty("private").GetBoolean();
    }

    internal async Task<bool> HasPublisherDispatchSecretAccessAsync(string repo)
    {
        var grants = await GhJsonAsync("orgs/AtyaLibraries/actions/secrets/PUBLISHER_DISPATCH_TOKEN/repositories?per_page=100").ConfigureAwait(false);
        return grants.RootElement.GetProperty("repositories").EnumerateArray()
            .Any(repository => StringComparer.OrdinalIgnoreCase.Equals(repository.GetProperty("full_name").GetString(), $"AtyaLibraries/{repo}"));
    }

    internal async Task<string?> GetDevelopmentBaselineAsync(string repo)
    {
        var tree = await GhJsonAsync($"repos/AtyaLibraries/{repo}/git/trees/development?recursive=1").ConfigureAwait(false);
        var csprojPath = tree.RootElement.GetProperty("tree").EnumerateArray()
            .Select(static e => e.GetProperty("path").GetString())
            .FirstOrDefault(static path => path is not null && path.StartsWith("src/", StringComparison.Ordinal) && path.EndsWith(".csproj", StringComparison.Ordinal));
        if (csprojPath is null)
        {
            return null;
        }

        var content = await GhJsonAsync($"repos/AtyaLibraries/{repo}/contents/{csprojPath}?ref=development").ConfigureAwait(false);
        var encoded = content.RootElement.GetProperty("content").GetString()?.Replace("\n", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        var xml = XDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
        return xml.Descendants().FirstOrDefault(static e => e.Name.LocalName == "PackageValidationBaselineVersion")?.Value.Trim();
    }

    internal async Task<IReadOnlyList<string>> FindStaleAtyaDependenciesAsync(RepositoryInfo repository)
    {
        var propsPath = Path.Combine(repository.Root, "Directory.Packages.props");
        if (!File.Exists(propsPath))
        {
            return Array.Empty<string>();
        }

        var xml = XDocument.Load(propsPath);
        var stale = new List<string>();
        foreach (var pin in xml.Descendants().Where(static e => e.Name.LocalName == "PackageVersion"))
        {
            var id = pin.Attribute("Include")?.Value;
            var version = pin.Attribute("Version")?.Value;
            if (id is null || version is null || !id.StartsWith("Atya.", StringComparison.Ordinal))
            {
                continue;
            }

            var latest = await GetLatestStableVersionAsync(id).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(latest) && !StringComparer.OrdinalIgnoreCase.Equals(version, latest))
            {
                stale.Add($"{id} pinned {version}, latest {latest}");
            }
        }

        return stale;
    }

    private async Task<string[]> GetVersionsAsync(string packageId)
    {
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";
        using var response = await s_http.GetAsync(url).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Array.Empty<string>();
        }

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return json.RootElement.GetProperty("versions").EnumerateArray().Select(static e => e.GetString()).Where(static v => v is not null).Cast<string>().ToArray();
    }

    private async Task<string?> GetBranchTreeShaAsync(string repo, string branch)
    {
        var reference = await GhJsonAsync($"repos/AtyaLibraries/{repo}/git/ref/heads/{branch}").ConfigureAwait(false);
        var commitSha = reference.RootElement.GetProperty("object").GetProperty("sha").GetString();
        if (commitSha is null)
        {
            return null;
        }

        var commit = await GhJsonAsync($"repos/AtyaLibraries/{repo}/git/commits/{commitSha}").ConfigureAwait(false);
        return commit.RootElement.GetProperty("tree").GetProperty("sha").GetString();
    }

    private async Task<bool> IsBranchCiGreenAsync(string repo, string branch)
    {
        var runs = await GhJsonAsync($"repos/AtyaLibraries/{repo}/actions/workflows/ci.yml/runs?branch={branch}&per_page=1").ConfigureAwait(false);
        var run = runs.RootElement.GetProperty("workflow_runs").EnumerateArray().FirstOrDefault();
        return run.ValueKind != JsonValueKind.Undefined &&
            run.GetProperty("status").GetString() == "completed" &&
            run.GetProperty("conclusion").GetString() == "success";
    }

    private static async Task<JsonDocument> GhJsonAsync(string path)
    {
        var result = await GhAsync("api", path).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"gh api failed for {path}: {result.Error}");
        }

        return JsonDocument.Parse(result.Output);
    }

    private static async Task<ProcessResult> GhAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo("gh")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start gh.");
        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, output, error);
    }

    internal sealed class BranchTrees
    {
        internal BranchTrees(string? developmentTreeSha, string? masterTreeSha)
        {
            DevelopmentTreeSha = developmentTreeSha;
            MasterTreeSha = masterTreeSha;
        }

        internal string? DevelopmentTreeSha { get; }

        internal string? MasterTreeSha { get; }
    }

    private sealed class ProcessResult
    {
        internal ProcessResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        internal int ExitCode { get; }

        internal string Output { get; }

        internal string Error { get; }
    }
}
