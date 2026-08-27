using System.Text.Json;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

/// <summary>
/// Loads and evaluates authoritative component metadata used by update planning.
/// Compatibility rules are data-driven and are never guessed from component names.
/// </summary>
public sealed class ComponentCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ComponentCatalog Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Component catalog was not found.", path);
        return Parse(File.ReadAllText(path));
    }

    public ComponentCatalog Parse(string json)
    {
        var catalog = JsonSerializer.Deserialize<ComponentCatalog>(json, JsonOptions)
            ?? throw new InvalidDataException("Component catalog is empty or invalid.");
        if (catalog.SchemaVersion < 1) throw new InvalidDataException("Unsupported component catalog schema.");
        if (catalog.Components is null) throw new InvalidDataException("Component catalog has no component list.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in catalog.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Id) || !ids.Add(component.Id))
                throw new InvalidDataException($"Duplicate or empty component id: {component.Id}");
            if (component.Dependencies.Any(string.IsNullOrWhiteSpace) || component.Conflicts.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Component {component.Id} contains an empty dependency/conflict.");
        }
        return catalog;
    }

    public ComponentUpdatePlan BuildPlan(ComponentCatalog catalog, IEnumerable<string> requested, IReadOnlyDictionary<string, string> installedVersions)
    {
        var entries = catalog.Components.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<ComponentCatalogEntry>();
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var incompatible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!entries.TryGetValue(id, out var entry)) { missing.Add(id); return; }
            if (!visiting.Add(id)) { cycles.Add(id); return; }

            foreach (var dependency in entry.Dependencies) Visit(dependency);
            visiting.Remove(id);
            visited.Add(id);

            if (entry.SupportedVersion is not null && installedVersions.TryGetValue(id, out var installed) &&
                !installed.Equals("installed", StringComparison.OrdinalIgnoreCase) &&
                !entry.SupportedVersion.Allows(installed))
                incompatible.Add(id);

            foreach (var conflict in entry.Conflicts)
                if (installedVersions.ContainsKey(conflict)) conflicts.Add($"{id}:{conflict}");

            if (!installedVersions.ContainsKey(id)) ordered.Add(entry);
        }

        foreach (var id in requested.Where(x => !string.IsNullOrWhiteSpace(x))) Visit(id);

        return new ComponentUpdatePlan(
            ordered.DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray(),
            missing.ToArray(), incompatible.ToArray(), conflicts.ToArray(), cycles.ToArray());
    }
}
