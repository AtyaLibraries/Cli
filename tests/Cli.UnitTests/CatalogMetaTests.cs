using System.Reflection;

namespace Atya.Tooling.Cli.UnitTests;

public sealed class CatalogMetaTests
{
    [Fact]
    public void CheckCatalog_Maps_Every_Diagnostic_Code_Constant_Exactly_Once()
    {
        var constants = typeof(DiagnosticCodes)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)
            .Where(static field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(static field => (string)field.GetRawConstantValue()!)
            .OrderBy(static code => code, StringComparer.Ordinal)
            .ToArray();

        var catalogCodes = CheckCatalog.All
            .Select(static descriptor => descriptor.Code)
            .OrderBy(static code => code, StringComparer.Ordinal)
            .ToArray();

        catalogCodes.Should().BeEquivalentTo(constants);
        CheckCatalog.All.GroupBy(static descriptor => descriptor.Code)
            .Should().OnlyContain(static group => group.Count() == 1);
    }

    [Fact]
    public void CheckCatalog_Entries_Declare_Constitution_Sections()
    {
        CheckCatalog.All.Should().OnlyContain(static descriptor =>
            !string.IsNullOrWhiteSpace(descriptor.ConstitutionSection));
    }
}
