using System.Text.Json;

namespace Atya.Tooling.Cli.UnitTests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task Main_Version_Prints_Informational_Version()
    {
        var result = await CaptureAsync("--version");

        result.ExitCode.Should().Be(CliExitCodes.Success);
        result.Output.Trim().Should().NotBeNullOrWhiteSpace();
        result.Error.Should().BeEmpty();
    }

    [Theory]
    [InlineData("init")]
    [InlineData("add", "code-quality")]
    [InlineData("pack")]
    [InlineData("generate", "readme")]
    [InlineData("publish")]
    public async Task Main_Cut_Command_Is_Refused(params string[] args)
    {
        var result = await CaptureAsync(args);

        result.ExitCode.Should().Be(CliExitCodes.InvalidArguments);
        result.Error.Should().Contain("cut from Atya CLI v1");
    }

    [Fact]
    public async Task Doctor_Json_Output_Is_Parseable_And_Offline()
    {
        var root = FindRepositoryRoot();

        var result = await CaptureAsync("--format", "json", "--ci", "--working-directory", root, "doctor");

        result.ExitCode.Should().Be(CliExitCodes.Success);
        result.Error.Should().BeEmpty();
        using var json = JsonDocument.Parse(result.Output);
        json.RootElement.GetProperty("workingDirectory").GetString().Should().Be(root);
        json.RootElement.GetProperty("findings").EnumerateArray()
            .Select(static finding => finding.GetProperty("code").GetString())
            .Should().Contain("REL-001");
    }

    private static async Task<CommandResult> CaptureAsync(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = await Program.Main(args).ConfigureAwait(false);
            return new CommandResult(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cli.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return directory!.FullName;
    }

    private sealed class CommandResult
    {
        internal CommandResult(int exitCode, string output, string error)
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
