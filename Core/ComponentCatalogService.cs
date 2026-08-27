using System.Text.Json;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public enum CatalogComponentState
{
    Missing,
    Installed,
    UpdateAvailable,
    Incompatible,
    Broken,
    Unknown
}

public sealed record CatalogComponent(
    string Id,
    string Name,
    string Channel,
    IReadOnlyList<string> Dependencies,
    VersionConstraint? Constraint);

public sealed record CatalogSnapshot(
    int SchemaVersion,
    string Product,
    IReadOnlyList<CatalogComponent> Components,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Loads and validates the MewNX component catalog. It is deliberately source-neutral:
/// release discovery is handled by the corresponding release client, while this service
/// owns catalog structure, dependency references and version constraints.
/// </summary>
public sealed class ComponentCatalogService
{
    public CatalogSnapshot Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return new(0, "MewNX", Array.Empty<CatalogComponent>(), new[] { $"Catalog not found: {manifestPath}" });

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var schema = root.TryGetProperty("schemaVersion", out var schemaValue) && schemaValue.TryGetInt32(out var schemaVersion) ? schemaVersion : 0;
            var product = root.TryGetProperty("product", out var productValue) ? productValue.GetString() ?? "MewNX" : "MewNX";
            if (!root.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
                return new(schema, product, Array.Empty<CatalogComponent>(), new[] { "Catalog has no components array." });

            var errors = new List<string>();
            var result = new List<CatalogComponent>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in components.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
                var name = item.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
                var channel = item.TryGetProperty("channel", out var channelValue) ? channelValue.GetString() : null;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    errors.Add("Catalog contains a component without id or name.");
                    continue;
                }
                if (!ids.Add(id)) errors.Add($"Catalog contains duplicate component id: {id}.");

                var dependencies = item.TryGetProperty("dependencies", out var dependencyValue) && dependencyValue.ValueKind == JsonValueKind.Array
                    ? dependencyValue.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    : Array.Empty<string>();

                VersionConstraint? constraint = null;
                if (item.TryGetProperty("versionConstraint", out var constraintValue) && constraintValue.ValueKind == JsonValueKind.Object)
                {
                    try { constraint = JsonSerializer.Deserialize<VersionConstraint>(constraintValue.GetRawText()); }
                    catch (JsonException ex) { errors.Add($"Invalid versionConstraint for {id}: {ex.Message}"); }
                }

                result.Add(new CatalogComponent(id, name, string.IsNullOrWhiteSpace(channel) ? "stable" : channel, dependencies, constraint));
            }

            foreach (var component in result)
                foreach (var dependency in component.Dependencies)
                    if (!ids.Contains(dependency)) errors.Add($"Component {component.Id} references missing dependency {dependency}.");

            return new(schema, product, result, errors.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch (JsonException ex)
        {
            return new(0, "MewNX", Array.Empty<CatalogComponent>(), new[] { $"Catalog JSON is invalid: {ex.Message}" });
        }
        catch (IOException ex)
        {
            return new(0, "MewNX", Array.Empty<CatalogComponent>(), new[] { $"Catalog could not be read: {ex.Message}" });
        }
    }

    public DependencyPlan BuildPlan(CatalogSnapshot snapshot, IEnumerable<string> requested, IReadOnlyDictionary<string, string> installedVersions)
    {
        if (!snapshot.IsValid) throw new InvalidDataException("Cannot build a dependency plan from an invalid component catalog.");
        var entries = snapshot.Components.Select(x => new ComponentManifestEntry(x.Id, x.Name, x.Channel, x.Dependencies, x.Constraint));
        return new DependencyManager().BuildPlan(entries, requested, installedVersions);
    }
}
