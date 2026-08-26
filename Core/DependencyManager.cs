using System.Text.Json;

namespace MewSwitchManager.Core;

public sealed record ComponentManifestEntry(string Id, string Name, string Channel, IReadOnlyList<string> Dependencies);
public sealed record DependencyPlan(IReadOnlyList<string> InstallOrder, IReadOnlyList<string> Missing, IReadOnlyList<string> Cycles);

public sealed class DependencyManager
{
    public IReadOnlyList<ComponentManifestEntry> Load(string manifestPath)
    {
        using var stream = File.OpenRead(manifestPath);
        var document = JsonDocument.Parse(stream);
        var list = new List<ComponentManifestEntry>();
        foreach (var item in document.RootElement.GetProperty("components").EnumerateArray())
        {
            var deps = item.TryGetProperty("dependencies", out var d)
                ? d.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToArray()
                : Array.Empty<string>();
            list.Add(new(item.GetProperty("id").GetString()!, item.GetProperty("name").GetString()!, item.GetProperty("channel").GetString() ?? "stable", deps));
        }
        return list;
    }

    public DependencyPlan BuildPlan(IEnumerable<ComponentManifestEntry> manifest, IEnumerable<string> requested, ISet<string> installed)
    {
        var entries = manifest.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var missing = new List<string>();
        var cycles = new List<string>();
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
            if (!installed.Contains(id)) order.Add(id);
        }

        foreach (var id in requested) Visit(id);
        return new(order, missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), cycles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
