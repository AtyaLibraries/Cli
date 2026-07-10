using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Loggers;

namespace Atya.Tooling.Cli.Benchmarks;

/// <summary>
/// Shared BenchmarkDotNet configuration for Atya benchmark suites: consistent columns,
/// a GitHub-markdown exporter (paste-able results) and a full JSON exporter (regression tooling).
/// Put <c>[MemoryDiagnoser]</c> on each benchmark class — allocations are first-class for Atya
/// libraries. The default job applies; override it from the CLI (e.g. <c>--job Short</c> / <c>--job Dry</c>).
/// </summary>
public sealed class BenchmarkConfig : ManualConfig
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkConfig"/> class.
    /// </summary>
    public BenchmarkConfig()
    {
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddLogger(ConsoleLogger.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.Full);
        WithOptions(ConfigOptions.DisableLogFile);
    }
}
