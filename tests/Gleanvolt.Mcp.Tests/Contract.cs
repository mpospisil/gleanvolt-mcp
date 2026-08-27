using System.Text.Json;

namespace Gleanvolt.Mcp.Tests;

/// <summary>
/// The checked-in OpenAPI document, loaded once. It travels with this repository rather than being
/// fetched from a running installation so the suite is reproducible, runs with the Pi switched off,
/// and — the point — so that updating it is a reviewable diff.
/// </summary>
internal static class Contract
{
    internal static readonly JsonElement Document = Load();

    /// <summary>Walks up from the test binary to the repository root, which is where contract/ lives.</summary>
    private static JsonElement Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "contract")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("No contract/ directory above the test binary."),
            "contract",
            "openapi.json");

        return JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));
    }

    internal static JsonElement Paths => Document.GetProperty("paths");

    internal static JsonElement Schemas => Document.GetProperty("components").GetProperty("schemas");

    /// <summary>The property names a request schema accepts, following the one $ref level these use.</summary>
    internal static IReadOnlySet<string> PropertiesOf(string schemaName)
    {
        var schema = Schemas.GetProperty(schemaName);

        return schema.TryGetProperty("properties", out var properties)
            ? properties.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }
}
