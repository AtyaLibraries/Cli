#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Small JsonElement helper is covered through command-level smoke tests.")]
internal static class JsonExtensions
{
    internal static JsonElement? GetPropertyOrNull(this JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property) ? property : null;
    }
}
