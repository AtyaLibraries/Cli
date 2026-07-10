using System.Xml.Linq;

namespace Atya.Tooling.Cli.UnitTests;

public sealed class DoctorMatrixTests
{
    public static TheoryData<string, string, Action<string>> ErrorFixtures => new()
    {
        { "REPO-001", "root nuget.config", root => File.WriteAllText(Path.Combine(root, "nuget.config"), string.Empty) },
        { "REPO-002", "missing unit test project folder", root => Directory.Delete(Path.Combine(root, "tests", "Acid.UnitTests"), recursive: true) },
        { "REPO-003", "missing LICENSE", root => File.Delete(Path.Combine(root, "LICENSE")) },
        { "REPO-004", "missing packed README", root => File.Delete(Path.Combine(root, "src", "Acid", "README.md")) },
        { "SDK-001", "Atya.Build.Sdk below floor", root => WriteGlobalJson(root, buildSdk: "1.5.1") },
        { "SDK-002", "wrong .NET SDK pin", root => WriteGlobalJson(root, sdkVersion: "9.0.100") },
        { "SDK-003", "wrong project SDK", root => ReplaceInSourceProject(root, "Project Sdk=\"Atya.Build.Sdk\"", "Project Sdk=\"Microsoft.NET.Sdk\"") },
        { "SDK-004", "hardcoded Version", root => InsertSourceProperty(root, "Version", "1.0.0") },
        { "NAME-001", "invalid package id casing", root => SetIdentity(root, "atya.foundation.acid") },
        { "NAME-002", "namespace identity mismatch", root => SetSourceProperty(root, "RootNamespace", "Atya.Foundation.Other") },
        { "NAME-003", "unknown area", root => SetIdentity(root, "Atya.Unknown.Acid") },
        { "NAME-004", "forbidden final segment", root => SetIdentity(root, "Atya.Foundation.Core") },
        { "CPM-001", "central transitive pinning off", root => SetDirectoryPackagesProperty(root, "CentralPackageTransitivePinningEnabled", "false") },
        { "CPM-002", "SDK-injected package pin", root => AddPackageVersion(root, "MinVer", "6.0.0") },
        { "CPM-003", "missing package lock", root => File.Delete(Path.Combine(root, "tests", "Acid.UnitTests", "packages.lock.json")) },
        { "CPM-004", "prerelease Atya runtime pin", root => AddPackageVersion(root, "Atya.Foundation.Guards", "1.0.0-alpha.1") },
        { "CI-001", "missing CodeQL workflow", root => File.Delete(Path.Combine(root, ".github", "workflows", "codeql.yml")) },
        { "CI-002", "workflow uses branch ref", root => ReplaceInFile(Path.Combine(root, ".github", "workflows", "ci.yml"), "@950f73c1f1fc997bc2d5c83d68c1729f748a0f77", "@development") },
        { "CI-003", "coverage line minimum too low", root => ReplaceInFile(Path.Combine(root, ".github", "workflows", "ci.yml"), "coverage-line-min: 90", "coverage-line-min: 80") },
        { "CI-004", "direct NuGet API key", root => File.AppendAllText(Path.Combine(root, ".github", "workflows", "publish-nuget.yml"), "NUGET_API_KEY") },
        { "PKG-001", "missing PackageId", root => RemoveSourceProperty(root, "PackageId") },
        { "PKG-002", "wrong Authors", root => SetSourceProperty(root, "Authors", "Atya") },
        { "PKG-003", "placeholder Description", root => SetSourceProperty(root, "Description", "__TODO__") },
        { "PKG-004", "wrong RepositoryUrl", root => SetSourceProperty(root, "RepositoryUrl", "https://github.com/AtyaLibraries/Wrong") },
        { "PKG-005", "wrong license expression", root => SetSourceProperty(root, "PackageLicenseExpression", "Apache-2.0") },
        { "PKG-007", "per-repo package icon", root => InsertSourceProperty(root, "PackageIcon", "icon.png") },
    };

    [Fact]
    public async Task Doctor_Valid_Fixture_Has_No_Error_Findings()
    {
        using var fixture = DoctorFixture.Create();

        var result = await RunDoctorAsync(fixture.Root);

        result.Errors.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(ErrorFixtures))]
    public async Task Doctor_Seeded_Error_Fixture_Reports_Expected_Code(string expectedCode, string _, Action<string> seed)
    {
        using var fixture = DoctorFixture.Create();
        seed(fixture.Root);

        var result = await RunDoctorAsync(fixture.Root);

        result.Findings.Should().Contain(finding =>
            finding.Code == expectedCode &&
            finding.Severity == DiagnosticSeverity.Error);
    }

    private static async Task<CheckResult> RunDoctorAsync(string root)
    {
        var repository = new RepositoryScanner().Scan(root);
        return await new DoctorChecker(new OnlineChecks()).CheckAsync(repository, online: false).ConfigureAwait(false);
    }

    private static void WriteGlobalJson(string root, string sdkVersion = "10.0.301", string buildSdk = "1.5.2")
    {
        File.WriteAllText(
            Path.Combine(root, "global.json"),
            $$"""
            {
              "sdk": {
                "version": "{{sdkVersion}}",
                "rollForward": "latestPatch",
                "allowPrerelease": false
              },
              "msbuild-sdks": {
                "Atya.Build.Sdk": "{{buildSdk}}"
              }
            }
            """);
    }

    private static void SetIdentity(string root, string packageId)
    {
        SetSourceProperty(root, "PackageId", packageId);
        SetSourceProperty(root, "AssemblyName", packageId);
        SetSourceProperty(root, "RootNamespace", packageId);
    }

    private static void InsertSourceProperty(string root, string name, string value)
    {
        var path = SourceProjectPath(root);
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var propertyGroup = document.Descendants().First(element => element.Name.LocalName == "PropertyGroup");
        propertyGroup.Add(new XElement(name, value));
        document.Save(path);
    }

    private static void SetSourceProperty(string root, string name, string value)
    {
        var path = SourceProjectPath(root);
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var element = document.Descendants().First(item => item.Name.LocalName == name);
        element.Value = value;
        document.Save(path);
    }

    private static void RemoveSourceProperty(string root, string name)
    {
        var path = SourceProjectPath(root);
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        document.Descendants().First(item => item.Name.LocalName == name).Remove();
        document.Save(path);
    }

    private static void ReplaceInSourceProject(string root, string oldValue, string newValue)
        => ReplaceInFile(SourceProjectPath(root), oldValue, newValue);

    private static void ReplaceInFile(string path, string oldValue, string newValue)
        => File.WriteAllText(path, File.ReadAllText(path).Replace(oldValue, newValue, StringComparison.Ordinal));

    private static void SetDirectoryPackagesProperty(string root, string name, string value)
    {
        var path = Path.Combine(root, "Directory.Packages.props");
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var element = document.Descendants().First(item => item.Name.LocalName == name);
        element.Value = value;
        document.Save(path);
    }

    private static void AddPackageVersion(string root, string id, string version)
    {
        var path = Path.Combine(root, "Directory.Packages.props");
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var itemGroup = document.Descendants().First(item => item.Name.LocalName == "ItemGroup");
        itemGroup.Add(new XElement("PackageVersion", new XAttribute("Include", id), new XAttribute("Version", version)));
        document.Save(path);
    }

    private static string SourceProjectPath(string root) => Path.Combine(root, "src", "Acid", "Acid.csproj");

    private sealed class DoctorFixture : IDisposable
    {
        private DoctorFixture(string parent, string root)
        {
            Parent = parent;
            Root = root;
        }

        private string Parent { get; }

        internal string Root { get; }

        internal static DoctorFixture Create()
        {
            var parent = Path.Combine(Path.GetTempPath(), "atya-cli-doctor-" + Guid.NewGuid().ToString("N"));
            var root = Path.Combine(parent, "Acid");
            Directory.CreateDirectory(root);
            WriteValidRepository(root);
            return new DoctorFixture(parent, root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Parent))
            {
                Directory.Delete(Parent, recursive: true);
            }
        }

        private static void WriteValidRepository(string root)
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

            WriteGlobalJson(root);
            WriteDirectoryPackages(root);
            WriteSourceProject(root);
            WriteProject(Path.Combine(root, "tests", "Acid.UnitTests", "Acid.UnitTests.csproj"));
            WriteBenchmarkProject(root);
            WriteProject(Path.Combine(root, "samples", "Acid.Samples.Console", "Acid.Samples.Console.csproj"));
            File.WriteAllText(Path.Combine(root, "tests", "Directory.Build.props"), "<Project />");
            File.WriteAllText(Path.Combine(root, "src", "Acid", "README.md"), "# Acid");

            foreach (var projectDirectory in new[]
            {
                Path.Combine(root, "src", "Acid"),
                Path.Combine(root, "tests", "Acid.UnitTests"),
                Path.Combine(root, "benchmarks", "Acid.Benchmarks"),
                Path.Combine(root, "samples", "Acid.Samples.Console"),
            })
            {
                File.WriteAllText(Path.Combine(projectDirectory, "packages.lock.json"), "{}");
            }

            WriteWorkflows(root);
        }

        private static void WriteDirectoryPackages(string root)
        {
            File.WriteAllText(
                Path.Combine(root, "Directory.Packages.props"),
                """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />
                  </ItemGroup>
                </Project>
                """);
        }

        private static void WriteSourceProject(string root)
        {
            File.WriteAllText(
                SourceProjectPath(root),
                """
                <Project Sdk="Atya.Build.Sdk">
                  <PropertyGroup>
                    <AssemblyName>Atya.Foundation.Acid</AssemblyName>
                    <PackageId>Atya.Foundation.Acid</PackageId>
                    <RootNamespace>Atya.Foundation.Acid</RootNamespace>
                    <IsPackable>true</IsPackable>
                    <Authors>Arsen Asulyan</Authors>
                    <Description>Acid fixture package.</Description>
                    <RepositoryUrl>https://github.com/AtyaLibraries/Acid</RepositoryUrl>
                    <PackageLicenseExpression>MIT</PackageLicenseExpression>
                  </PropertyGroup>
                </Project>
                """);
        }

        private static void WriteBenchmarkProject(string root)
        {
            File.WriteAllText(
                Path.Combine(root, "benchmarks", "Acid.Benchmarks", "Acid.Benchmarks.csproj"),
                """
                <Project Sdk="Atya.Build.Sdk">
                  <ItemGroup>
                    <PackageReference Include="BenchmarkDotNet" />
                  </ItemGroup>
                </Project>
                """);
        }

        private static void WriteProject(string path)
            => File.WriteAllText(path, "<Project Sdk=\"Atya.Build.Sdk\" />");

        private static void WriteWorkflows(string root)
        {
            File.WriteAllText(
                Path.Combine(root, ".github", "workflows", "ci.yml"),
                """
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
