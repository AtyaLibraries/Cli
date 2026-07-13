using System.Text.Json;

namespace Atya.Tooling.Cli.UnitTests;

public sealed class GoldenBaselineTests
{
    [Theory]
    [InlineData("cli-repo-doctor-json.golden.json", true, "--format", "json", "--ci")]
    [InlineData("cli-repo-doctor-text.golden.txt", true, "--ci")]
    public async Task Doctor_Cli_Repo_Output_Matches_Golden_Baseline(string goldenFile, bool _, params string[] options)
    {
        var root = FindRepositoryRoot();

        var result = await CaptureDoctorAsync(root, options);

        Normalize(result.Output, root).Should().Be(ReadGolden(goldenFile));
        result.Error.Should().BeEmpty();
    }

    [Theory]
    [InlineData("fresh-template-doctor-json.golden.json", "--format", "json", "--ci")]
    [InlineData("fresh-template-doctor-text.golden.txt", "--ci")]
    public async Task Doctor_Fresh_Template_Output_Matches_Golden_Baseline(string goldenFile, params string[] options)
    {
        using var fixture = FreshTemplateFixture.Create();

        var result = await CaptureDoctorAsync(fixture.Root, options);

        Normalize(result.Output, fixture.Root).Should().Be(ReadGolden(goldenFile));
        result.Error.Should().BeEmpty();
    }

    [Fact]
    public async Task Doctor_Json_Format_Emits_Json_Only()
    {
        var root = FindRepositoryRoot();

        var result = await CaptureDoctorAsync(root, "--format", "json", "--ci");

        result.Output.TrimStart().Should().StartWith("{");
        result.Output.TrimEnd().Should().EndWith("}");
        JsonDocument.Parse(result.Output).RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        result.Error.Should().BeEmpty();
    }

    private static async Task<CommandResult> CaptureDoctorAsync(string root, params string[] options)
    {
        var args = options.Concat(["--working-directory", root, "doctor"]).ToArray();
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

    private static string ReadGolden(string fileName)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tests", "GoldenBaselines", fileName)).ReplaceLineEndings("\n");

    private static string Normalize(string output, string root)
    {
        var normalized = output
            .Replace(root.Replace(@"\", @"\\"), "<WORKING_DIRECTORY>", StringComparison.Ordinal)
            .Replace(root, "<WORKING_DIRECTORY>", StringComparison.Ordinal)
            .ReplaceLineEndings("\n");
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"<WORKING_DIRECTORY>(?:\\\\|\\|/)([^"",\n]*)",
            static match => "<WORKING_DIRECTORY>/" + match.Groups[1].Value.Replace(@"\\", "/", StringComparison.Ordinal).Replace(@"\", "/", StringComparison.Ordinal));
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @", \d+ passed,", ", <PASSED> passed,");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"""passed"": \d+", @"""passed"": ""<PASSED>""");
        return normalized;
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

    private sealed class FreshTemplateFixture : IDisposable
    {
        private FreshTemplateFixture(string parent, string root)
        {
            Parent = parent;
            Root = root;
        }

        private string Parent { get; }

        internal string Root { get; }

        internal static FreshTemplateFixture Create()
        {
            var parent = Path.Combine(Path.GetTempPath(), "atya-cli-fresh-template-" + Guid.NewGuid().ToString("N"));
            var root = Path.Combine(parent, "Acid");
            Directory.CreateDirectory(root);
            WriteRepository(root);
            return new FreshTemplateFixture(parent, root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Parent))
            {
                Directory.Delete(Parent, recursive: true);
            }
        }

        private static void WriteRepository(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, ".github", "workflows"));
            Directory.CreateDirectory(Path.Combine(root, "src", "Acid"));
            Directory.CreateDirectory(Path.Combine(root, "tests", "Acid.UnitTests"));
            Directory.CreateDirectory(Path.Combine(root, "benchmarks", "Acid.Benchmarks"));
            Directory.CreateDirectory(Path.Combine(root, "samples", "Acid.Samples.Console"));

            foreach (var file in new[] { ".editorconfig", ".gitattributes", ".gitignore", "renovate.json", "LICENSE", "README.md", "Acid.sln" })
            {
                File.WriteAllText(Path.Combine(root, file), "placeholder");
            }

            File.WriteAllText(Path.Combine(root, "global.json"), """
                {
                  "sdk": {
                    "version": "10.0.301",
                    "rollForward": "latestPatch",
                    "allowPrerelease": false
                  },
                  "msbuild-sdks": {
                    "Atya.Build.Sdk": "1.5.2"
                  }
                }
                """);
            File.WriteAllText(Path.Combine(root, "Directory.Packages.props"), """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Atya.Foundation.Guards" Version="1.0.0" />
                    <PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(root, "src", "Acid", "Acid.csproj"), """
                <Project Sdk="Atya.Build.Sdk">
                  <PropertyGroup>
                    <AssemblyName>Atya.Acid</AssemblyName>
                    <PackageId>Atya.Acid</PackageId>
                    <RootNamespace>Atya.Acid</RootNamespace>
                    <IsPackable>true</IsPackable>
                    <Authors>Arsen Asulyan</Authors>
                    <Description>Acid fixture package.</Description>
                    <RepositoryUrl>https://github.com/AtyaLibraries/Atya.Acid</RepositoryUrl>
                    <PackageLicenseExpression>MIT</PackageLicenseExpression>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Atya.Foundation.Guards" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(root, "tests", "Acid.UnitTests", "Acid.UnitTests.csproj"), "<Project Sdk=\"Atya.Build.Sdk\" />");
            File.WriteAllText(Path.Combine(root, "benchmarks", "Acid.Benchmarks", "Acid.Benchmarks.csproj"), """
                <Project Sdk="Atya.Build.Sdk">
                  <ItemGroup>
                    <PackageReference Include="BenchmarkDotNet" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(root, "samples", "Acid.Samples.Console", "Acid.Samples.Console.csproj"), "<Project Sdk=\"Atya.Build.Sdk\" />");
            File.WriteAllText(Path.Combine(root, "tests", "Directory.Build.props"), "<Project />");
            File.WriteAllText(Path.Combine(root, "src", "Acid", "README.md"), "# Acid");

            foreach (var projectDirectory in new[] { "src/Acid", "tests/Acid.UnitTests", "benchmarks/Acid.Benchmarks", "samples/Acid.Samples.Console" })
            {
                File.WriteAllText(Path.Combine(root, projectDirectory.Replace('/', Path.DirectorySeparatorChar), "packages.lock.json"), "{}");
            }

            WriteWorkflows(root);
        }

        private static void WriteWorkflows(string root)
        {
            File.WriteAllText(Path.Combine(root, ".github", "workflows", "ci.yml"), """
                jobs:
                  ci:
                    uses: AtyaLibraries/github-workflows/.github/workflows/dotnet-package-ci.yml@950f73c1f1fc997bc2d5c83d68c1729f748a0f77
                    with:
                      fail-on-coverage: true
                      coverage-line-min: 90
                      coverage-branch-min: 80
                """);
            File.WriteAllText(Path.Combine(root, ".github", "workflows", "codeql.yml"), "name: CodeQL");
            File.WriteAllText(Path.Combine(root, ".github", "workflows", "dependency-review.yml"), "name: Dependency Review");
            File.WriteAllText(Path.Combine(root, ".github", "workflows", "publish-nuget.yml"), "name: Publish");
        }
    }
}
