using System.Text.Json;
using MewNX.Models;

namespace MewNX.Core;

public sealed class ComponentCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ComponentCatalog Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Component catalog was not found.", path);
        return Parse(File.ReadAllText(path));
    }

    public ComponentCatalog Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var catalog = JsonSerializer.Deserialize<ComponentCatalog>(json, JsonOptions)
            ?? throw new InvalidDataException("Component catalog is empty or invalid.");
        ValidateCatalog(catalog);
        return catalog;
    }

    private static void ValidateCatalog(ComponentCatalog catalog)
    {
        if (catalog.SchemaVersion < 1)
            throw new InvalidDataException("Unsupported component catalog schema.");
        if (catalog.Components is null)
            throw new InvalidDataException("Component catalog has no component list.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in catalog.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Id) || !ids.Add(component.Id))
                throw new InvalidDataException($"Duplicate or empty component id: {component.Id}");
            if (string.IsNullOrWhiteSpace(component.Name))
                throw new InvalidDataException($"Component {component.Id} has no name.");
            if (component.Dependencies.Any(string.IsNullOrWhiteSpace) || component.Conflicts.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Component {component.Id} contains an empty dependency/conflict.");
        }

        foreach (var component in catalog.Components)
        {
            ValidateReferences(component.Id, component.Dependencies, ids, "dependency");
            ValidateReferences(component.Id, component.Conflicts, ids, "conflict");
        }
    }

    private static void ValidateReferences(string componentId, IEnumerable<string> references, HashSet<string> ids, string kind)
    {
        foreach (var reference in references)
            if (!ids.Contains(reference))
                throw new InvalidDataException($"Component {componentId} references missing {kind} {reference}.");
    }

    public ComponentUpdatePlan BuildPlan(
        ComponentCatalog catalog,
        IEnumerable<string> requested,
        IReadOnlyDictionary<string, string> installedVersions)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(installedVersions);

        var entries = catalog.Components.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<ComponentCatalogEntry>();
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var incompatible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Visit(string id)
        {
            if (visited.Contains(id)) return !blocked.Contains(id);
            if (!entries.TryGetValue(id, out var entry))
            {
                missing.Add(id);
                blocked.Add(id);
                return false;
            }
            if (!visiting.Add(id))
            {
                cycles.Add(id);
                blocked.Add(id);
                return false;
            }

            var healthy = true;
            foreach (var dependency in entry.Dependencies)
                if (!Visit(dependency)) healthy = false;

            visiting.Remove(id);
            visited.Add(id);

            if (entry.SupportedVersion is not null && installedVersions.TryGetValue(id, out var installed) &&
                !IsMarkerVersion(installed) && !entry.SupportedVersion.Allows(installed))
            {
                incompatible.Add(id);
                healthy = false;
            }

            foreach (var conflict in entry.Conflicts)
            {
                if (!installedVersions.ContainsKey(conflict)) continue;
                conflicts.Add($"{id}:{conflict}");
                healthy = false;
            }

            if (!healthy)
            {
                blocked.Add(id);
                return false;
            }

            if (!installedVersions.TryGetValue(id, out var current) ||
                IsMarkerVersion(current) ||
                IsNewerThanLatest(current, entry.LatestVersion))
                ordered.Add(entry);

            return true;
        }

        foreach (var id in requested.Where(static x => !string.IsNullOrWhiteSpace(x)))
            Visit(id);

        return new ComponentUpdatePlan(
            ordered.DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray(),
            missing.ToArray(),
            incompatible.ToArray(),
            conflicts.ToArray(),
            cycles.ToArray());
    }

    private static bool IsMarkerVersion(string value)
        => string.Equals(value, "installed", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "detected", StringComparison.OrdinalIgnoreCase);

    private static bool IsNewerThanLatest(string current, string? latest)
    {
        if (string.IsNullOrWhiteSpace(latest) || IsMarkerVersion(current)) return false;
        return VersionConstraintParser.Compare(current, latest) < 0;
    }
}
