using System.Text.Json;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed record ComponentManifestEntry(string Id, string Name, string Channel, IReadOnlyList<string> Dependencies, VersionConstraint? Constraint = null);
public sealed record DependencyPlan(IReadOnlyList<string> InstallOrder, IReadOnlyList<string> Missing, IReadOnlyList<string> Cycles, IReadOnlyList<string> Incompatible);

public sealed class DependencyManager
{
    public IReadOnlyList<ComponentManifestEntry> Load(string manifestPath)
    {
        using var stream = File.OpenRead(manifestPath);
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Dependency manifest has no components array.");

        var list = new List<ComponentManifestEntry>();
        foreach (var item in components.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            var name = item.GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException("Dependency manifest contains a component without id or name.");
            var deps = item.TryGetProperty("dependencies", out var d) && d.ValueKind == JsonValueKind.Array
                ? d.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray()
                : Array.Empty<string>();
            VersionConstraint? constraint = null;
            if (item.TryGetProperty("versionConstraint", out var c) && c.ValueKind == JsonValueKind.Object)
                constraint = JsonSerializer.Deserialize<VersionConstraint>(c.GetRawText());
            list.Add(new(id, name, item.GetProperty("channel").GetString() ?? "stable", deps, constraint));
        }
        return list;
    }

    public DependencyPlan BuildPlan(IEnumerable<ComponentManifestEntry> manifest, IEnumerable<string> requested, ISet<string> installed)
        => BuildPlan(manifest, requested, installed.ToDictionary(x => x, _ => "installed", StringComparer.OrdinalIgnoreCase));

    public DependencyPlan BuildPlan(IEnumerable<ComponentManifestEntry> manifest, IEnumerable<string> requested, IReadOnlyDictionary<string, string> installedVersions)
    {
        var entries = manifest.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var missing = new List<string>();
        var cycles = new List<string>();
        var incompatible = new List<string>();
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

            if (!installedVersions.TryGetValue(id, out var installedVersion))
            {
                order.Add(id);
                return;
            }
            if (entry.Constraint is not null && !installedVersion.Equals("installed", StringComparison.OrdinalIgnoreCase) && !entry.Constraint.Allows(installedVersion))
                incompatible.Add(id);
        }

        foreach (var id in requested.Where(x => !string.IsNullOrWhiteSpace(x))) Visit(id);
        return new(order.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), cycles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), incompatible.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
