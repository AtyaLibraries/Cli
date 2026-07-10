using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await CliApplication.RunAsync(args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled exception: {ex.Message}");
            return CliExitCodes.UnhandledException;
        }
    }
}

/// <summary>
/// Marker type for locating the Atya.Tooling.Cli assembly.
/// </summary>
public static class CliMarker
{
    /// <summary>
    /// Gets the package identifier used by the Atya CLI tool.
    /// </summary>
    public static string PackageId => "Atya.Tooling.Cli";
}

internal static class CliExitCodes
{
    internal const int Success = 0;
    internal const int ValidationFailed = 1;
    internal const int InvalidArguments = 2;
    internal const int ExternalCommandFailed = 3;
    internal const int NotAnAtyaRepository = 4;
    internal const int UnhandledException = 10;
}

internal enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

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

internal sealed class CliOptions
{
    internal CliOptions(
        string format,
        string workingDirectory,
        bool verbose,
        bool noColor,
        bool ci,
        bool warningsAsErrors,
        bool online,
        string? version,
        bool allowMajor)
    {
        Format = format;
        WorkingDirectory = workingDirectory;
        Verbose = verbose;
        NoColor = noColor;
        Ci = ci;
        WarningsAsErrors = warningsAsErrors;
        Online = online;
        Version = version;
        AllowMajor = allowMajor;
    }

    internal string Format { get; }

    internal string WorkingDirectory { get; }

    internal bool Verbose { get; }

    internal bool NoColor { get; }

    internal bool Ci { get; }

    internal bool WarningsAsErrors { get; }

    internal bool Online { get; }

    internal string? Version { get; }

    internal bool AllowMajor { get; }
}

internal static class CliApplication
{
    private const string JsonFormat = "json";
    private const string TextFormat = "text";

    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            WriteUsage(Console.Error);
            return CliExitCodes.InvalidArguments;
        }

        if (args is ["--version"])
        {
            Console.WriteLine(GetVersion());
            return CliExitCodes.Success;
        }

        var parsed = Parse(args);
        if (!parsed.Success)
        {
            Console.Error.WriteLine(parsed.Error);
            return CliExitCodes.InvalidArguments;
        }

        var options = parsed.Options!;
        var command = parsed.Command;
        if (command.Count == 0)
        {
            WriteUsage(Console.Error);
            return CliExitCodes.InvalidArguments;
        }

        if (IsCutCommand(command))
        {
            Console.Error.WriteLine($"'{string.Join(' ', command)}' is cut from Atya CLI v1 by spec and is refused.");
            return CliExitCodes.InvalidArguments;
        }

        var fullPath = Path.GetFullPath(options.WorkingDirectory);
        if (!Directory.Exists(fullPath))
        {
            Console.Error.WriteLine($"Working directory does not exist: {fullPath}");
            return CliExitCodes.InvalidArguments;
        }

        var scanner = new RepositoryScanner();
        var repository = scanner.Scan(fullPath);
        if (repository.SourceProject is null)
        {
            Console.Error.WriteLine($"Not an Atya repository: {fullPath}");
            return CliExitCodes.NotAnAtyaRepository;
        }

        return command switch
        {
            ["doctor"] => await RunDoctorAsync(repository, options).ConfigureAwait(false),
            ["release", "check"] => await RunReleaseCheckAsync(repository, options).ConfigureAwait(false),
            ["release", "verify"] => await RunReleaseVerifyAsync(repository, options).ConfigureAwait(false),
            _ => InvalidCommand(command),
        };
    }

    private static async Task<int> RunDoctorAsync(RepositoryInfo repository, CliOptions options)
    {
        var checker = new DoctorChecker(new OnlineChecks());
        var result = await checker.CheckAsync(repository, options.Online).ConfigureAwait(false);
        OutputWriter.Write(result, repository, options);
        return ExitFromResult(result, options);
    }

    private static async Task<int> RunReleaseCheckAsync(RepositoryInfo repository, CliOptions options)
    {
        if (!ValidateOnlineReleaseOptions(options))
        {
            return CliExitCodes.InvalidArguments;
        }

        var checker = new ReleaseChecker(new OnlineChecks(), new DoctorChecker(new OnlineChecks()));
        var result = await checker.CheckAsync(repository, options.Version!, options.AllowMajor).ConfigureAwait(false);
        OutputWriter.Write(result, repository, options);
        return ExitFromResult(result, options);
    }

    private static async Task<int> RunReleaseVerifyAsync(RepositoryInfo repository, CliOptions options)
    {
        if (!ValidateOnlineReleaseOptions(options))
        {
            return CliExitCodes.InvalidArguments;
        }

        var checker = new ReleaseVerifier(new OnlineChecks());
        var result = await checker.VerifyAsync(repository, options.Version!).ConfigureAwait(false);
        OutputWriter.Write(result, repository, options);
        return ExitFromResult(result, options);
    }

    private static bool ValidateOnlineReleaseOptions(CliOptions options)
    {
        if (!options.Online)
        {
            Console.Error.WriteLine("release commands require --online.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            Console.Error.WriteLine("release commands require --version <vNew>.");
            return false;
        }

        return true;
    }

    private static int ExitFromResult(CheckResult result, CliOptions options)
    {
        if (result.Errors > 0 || (options.WarningsAsErrors && result.Warnings > 0))
        {
            return CliExitCodes.ValidationFailed;
        }

        return CliExitCodes.Success;
    }

    private static int InvalidCommand(IReadOnlyList<string> command)
    {
        Console.Error.WriteLine($"Unknown command: {string.Join(' ', command)}");
        return CliExitCodes.InvalidArguments;
    }

    private static bool IsCutCommand(IReadOnlyList<string> command)
    {
        if (command is ["init"] or ["pack"] or ["clean"])
        {
            return true;
        }

        if (command.Count >= 2 && command[0] == "add")
        {
            return true;
        }

        if (command.Count >= 1 && command[0] == "generate")
        {
            return true;
        }

        if (command.Count >= 1 && (command[0] == "publish" || command[0] == "tag"))
        {
            return true;
        }

        return false;
    }

    private static ParseResult Parse(string[] args)
    {
        var format = TextFormat;
        var workingDirectory = Directory.GetCurrentDirectory();
        var verbose = false;
        var noColor = false;
        var ci = false;
        var warningsAsErrors = false;
        var online = false;
        var version = default(string);
        var allowMajor = false;
        var command = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--format":
                    if (!TryReadValue(args, ref index, out format))
                    {
                        return ParseResult.Fail("--format requires text or json.");
                    }

                    if (format is not (TextFormat or JsonFormat))
                    {
                        return ParseResult.Fail("--format must be text or json.");
                    }

                    break;
                case "--working-directory":
                    if (!TryReadValue(args, ref index, out workingDirectory))
                    {
                        return ParseResult.Fail("--working-directory requires a path.");
                    }

                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--no-color":
                    noColor = true;
                    break;
                case "--ci":
                    ci = true;
                    noColor = true;
                    break;
                case "--warnings-as-errors":
                    warningsAsErrors = true;
                    break;
                case "--online":
                    online = true;
                    break;
                case "--version":
                    if (!TryReadValue(args, ref index, out version))
                    {
                        return ParseResult.Fail("--version requires a value in this command context.");
                    }

                    break;
                case "--allow-major":
                    allowMajor = true;
                    break;
                default:
                    command.Add(arg);
                    break;
            }
        }

        return ParseResult.Ok(
            command,
            new CliOptions(format, workingDirectory, verbose, noColor, ci, warningsAsErrors, online, version, allowMajor));
    }

    private static bool TryReadValue(string[] args, ref int index, out string value)
    {
        value = string.Empty;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static string GetVersion()
    {
        var version = typeof(CliMarker).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: atya [global options] doctor");
        writer.WriteLine("       atya [global options] release check --online --version <vNew>");
        writer.WriteLine("       atya [global options] release verify --online --version <vNew>");
    }

    private sealed class ParseResult
    {
        private ParseResult(bool success, IReadOnlyList<string> command, CliOptions? options, string? error)
        {
            Success = success;
            Command = command;
            Options = options;
            Error = error;
        }

        internal bool Success { get; }

        internal IReadOnlyList<string> Command { get; }

        internal CliOptions? Options { get; }

        internal string? Error { get; }

        internal static ParseResult Ok(IReadOnlyList<string> command, CliOptions options) => new(success: true, command, options, error: null);

        internal static ParseResult Fail(string error) => new(success: false, Array.Empty<string>(), options: null, error);
    }
}

internal sealed class RepositoryInfo
{
    internal RepositoryInfo(string root, string name, ProjectInfo? sourceProject, IReadOnlyList<ProjectInfo> projects, IReadOnlyDictionary<string, string> rootFiles)
    {
        Root = root;
        Name = name;
        SourceProject = sourceProject;
        Projects = projects;
        RootFiles = rootFiles;
    }

    internal string Root { get; }

    internal string Name { get; }

    internal ProjectInfo? SourceProject { get; }

    internal IReadOnlyList<ProjectInfo> Projects { get; }

    internal IReadOnlyDictionary<string, string> RootFiles { get; }
}

internal sealed class ProjectInfo
{
    internal ProjectInfo(
        string path,
        string directory,
        string fileName,
        string kind,
        XDocument? xml,
        IReadOnlyDictionary<string, string> properties,
        IReadOnlyList<string> packageReferences)
    {
        Path = path;
        Directory = directory;
        FileName = fileName;
        Kind = kind;
        Xml = xml;
        Properties = properties;
        PackageReferences = packageReferences;
    }

    internal string Path { get; }

    internal string Directory { get; }

    internal string FileName { get; }

    internal string Kind { get; }

    internal XDocument? Xml { get; }

    internal IReadOnlyDictionary<string, string> Properties { get; }

    internal IReadOnlyList<string> PackageReferences { get; }
}

internal sealed class RepositoryScanner
{
    internal RepositoryInfo Scan(string root)
    {
        var projects = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => LoadProject(root, path))
            .OrderBy(static p => p.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var source = projects.FirstOrDefault(static p => p.Kind == "Source");
        var rootFiles = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Select(static path => new { Name = Path.GetFileName(path), Path = path })
            .Where(static file => file.Name is not null)
            .ToDictionary(static file => file.Name!, static file => file.Path, StringComparer.OrdinalIgnoreCase);
        return new RepositoryInfo(root, new DirectoryInfo(root).Name, source, projects, rootFiles);
    }

    private static ProjectInfo LoadProject(string root, string path)
    {
        XDocument? xml = null;
        try
        {
            xml = XDocument.Load(path, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (InvalidOperationException)
        {
        }

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var packageReferences = new List<string>();
        if (xml is not null)
        {
            foreach (var element in xml.Descendants().Where(static e => e.Name.LocalName is not "Project" and not "PropertyGroup" and not "ItemGroup"))
            {
                if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
                {
                    properties[element.Name.LocalName] = element.Value.Trim();
                }
            }

            packageReferences.AddRange(xml.Descendants()
                .Where(static e => e.Name.LocalName == "PackageReference")
                .Select(static e => e.Attribute("Include")?.Value)
                .Where(static include => !string.IsNullOrWhiteSpace(include))!);
        }

        var relative = Path.GetRelativePath(root, path);
        var directory = Path.GetDirectoryName(path) ?? root;
        var normalized = relative.Replace(Path.DirectorySeparatorChar, '/');
        var kind = normalized.StartsWith("src/", StringComparison.OrdinalIgnoreCase)
            ? "Source"
            : normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)
                ? "Test"
                : normalized.StartsWith("benchmarks/", StringComparison.OrdinalIgnoreCase)
                    ? "Benchmark"
                    : normalized.StartsWith("samples/", StringComparison.OrdinalIgnoreCase)
                        ? "Sample"
                        : "Unknown";

        return new ProjectInfo(path, directory, Path.GetFileName(path), kind, xml, properties, packageReferences);
    }
}

[ExcludeFromCodeCoverage(Justification = "Doctor matrix is validated by CLI smoke tests in v1; fixture-per-code tests are planned next.")]
internal sealed class DoctorChecker
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
                else
                {
                    builder.AddPassed(1);
                }
            }
        }

        var requiredLayout = new[]
        {
            $"{repository.Name}.sln",
            "src/Cli",
            "tests/Cli.UnitTests",
            "benchmarks/Cli.Benchmarks",
            "samples/Cli.Samples.Console",
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

        builder.Expect(File.Exists(Combine(repository.Root, "src/Cli/README.md")), "REPO-004", DiagnosticSeverity.Error, "Packed NuGet README missing.", Combine(repository.Root, "src/Cli/README.md"));
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
        builder.Expect(source.Properties.TryGetValue("PackageLicenseExpression", out var license) && license == "MIT", "PKG-005", DiagnosticSeverity.Error, "PackageLicenseExpression must be MIT.", source.Path);
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

    private static string Combine(string root, string relative) => Path.Combine(relative.Split('/').Prepend(root).ToArray());

    private static bool TryReadJson(string path, out JsonDocument document)
    {
        document = null!;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            return true;
        }
        catch (Exception) when (!Debugger.IsAttached)
        {
            return false;
        }
    }

    private static bool TryReadXml(string path, out XDocument document)
    {
        document = null!;
        try
        {
            document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            return true;
        }
        catch (Exception) when (!Debugger.IsAttached)
        {
            return false;
        }
    }

    private static bool IsSdkVersion(string? version) => version is not null && Regex.IsMatch(version, @"^10\.0\.3\d\d$");

    private static bool IsAtLeast(string? value, Version minimum) => Version.TryParse(value, out var version) && version >= minimum;

    private static bool HasTrackedContent(string root, string relativePath)
    {
        if (!Directory.Exists(Path.Combine(root, ".git")))
        {
            return true;
        }

        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add($"safe.directory={root.Replace('\\', '/')}");
        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(relativePath.Replace('\\', '/'));

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return true;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode != 0 || !string.IsNullOrWhiteSpace(output);
    }

    private sealed class PackageVersionPin
    {
        internal PackageVersionPin(string id, string version)
        {
            Id = id;
            Version = version;
        }

        internal string Id { get; }

        internal string Version { get; }
    }
}

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

internal static partial class ConstitutionRules
{
    internal static readonly Regex s_packageIdRegex = PackageIdPattern();

    internal static readonly HashSet<string> s_allowedAreas = new(StringComparer.Ordinal)
    {
        "Foundation", "Errors", "Diagnostics", "Web", "Http", "Hosting", "Messaging",
        "Application", "Data", "Security", "Governance", "Build", "Templates", "Tooling",
    };

    internal static readonly HashSet<string> s_forbiddenFinalSegments = new(StringComparer.Ordinal)
    {
        "Core", "Utilities", "Helpers", "Common", "Shared", "Misc", "Constants",
    };

    internal static readonly string[] s_requiredRootFiles =
    [
        ".editorconfig",
        ".gitattributes",
        ".gitignore",
        "renovate.json",
        "LICENSE",
        "global.json",
        "Directory.Packages.props",
        "README.md",
    ];

    internal static bool IsSdkInjectedPackage(string packageId)
        => packageId.StartsWith("MinVer", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("Microsoft.SourceLink", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("coverlet", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("FluentAssertions", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("NSubstitute", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("Atya.Governance.", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^Atya\.[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)+$")]
    private static partial Regex PackageIdPattern();
}

internal static class JsonExtensions
{
    internal static JsonElement? GetPropertyOrNull(this JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property) ? property : null;
    }
}
