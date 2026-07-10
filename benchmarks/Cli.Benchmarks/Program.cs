using BenchmarkDotNet.Running;

namespace Atya.Tooling.Cli.Benchmarks;

/// <summary>
/// Runs the benchmark suite.
/// </summary>
public static class Program
{
    // BenchmarkSwitcher lets you filter/select from the CLI, e.g.:
    //   dotnet run -c Release -- --filter "*"            (run everything)
    //   dotnet run -c Release -- --filter "*" --job Dry  (fast smoke run)
    /// <summary>
    /// Starts the benchmark runner.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new BenchmarkConfig());
}
