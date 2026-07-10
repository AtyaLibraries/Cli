namespace Atya.Tooling.Cli.UnitTests;

public sealed class TemplateWiringTests
{
    [Fact]
    public void LibraryAssembly_Can_Be_Loaded()
    {
        var assembly = typeof(CliMarker).Assembly;

        assembly.Should().NotBeNull();
        assembly.GetName().Name.Should().Be("Atya.Tooling.Cli");
    }

    [Fact]
    public void Marker_Exposes_Package_Id()
    {
        CliMarker.PackageId.Should().Be("Atya.Tooling.Cli");
    }
}
