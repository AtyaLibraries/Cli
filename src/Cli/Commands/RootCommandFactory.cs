#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Command routing is covered by command-level smoke tests.")]
internal static class RootCommandFactory
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
