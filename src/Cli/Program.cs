#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "CLI process entry point is covered by command-level smoke tests.")]
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await RootCommandFactory.RunAsync(args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled exception: {ex.Message}");
            return CliExitCodes.UnhandledException;
        }
    }
}
