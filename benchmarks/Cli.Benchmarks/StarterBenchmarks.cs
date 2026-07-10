using BenchmarkDotNet.Attributes;

namespace Atya.Tooling.Cli.Benchmarks;

/// <summary>
/// Benchmarks the stable package metadata lookup path.
/// </summary>
[MemoryDiagnoser]
public class StarterBenchmarks
{
    private readonly Type _markerType = typeof(CliMarker);

    /// <summary>
    /// Reads the package identifier and assembly name.
    /// </summary>
    /// <returns>The combined metadata length.</returns>
    [Benchmark]
    public int ReadPackageMetadataLength() => CliMarker.PackageId.Length + _markerType.Assembly.GetName().Name!.Length;
}
