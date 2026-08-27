using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed record CatalogUpdateSelection(ComponentUpdatePlan Plan, IReadOnlyList<string> Blockers)
{
    public bool CanProceed => Plan.CanApply && Blockers.Count == 0;
}

/// <summary>Single gate between catalog planning and transactional execution.</summary>
public sealed class CatalogUpdateCoordinator
{
    private readonly ComponentCatalogService _catalog;
    public CatalogUpdateCoordinator(ComponentCatalogService catalog) => _catalog = catalog;

    public CatalogUpdateSelection Prepare(ComponentCatalog catalog, IEnumerable<string> requested, IReadOnlyDictionary<string, string> installedVersions)
    {
        var plan = _catalog.BuildPlan(catalog, requested, installedVersions);
        var blockers = new List<string>();
        if (plan.Missing.Count > 0) blockers.AddRange(plan.Missing.Select(x => $"Missing component or dependency: {x}"));
        if (plan.Incompatible.Count > 0) blockers.AddRange(plan.Incompatible.Select(x => $"Incompatible installed version: {x}"));
        if (plan.Conflicts.Count > 0) blockers.AddRange(plan.Conflicts.Select(x => $"Component conflict: {x}"));
        if (plan.Cycles.Count > 0) blockers.AddRange(plan.Cycles.Select(x => $"Dependency cycle: {x}"));
        return new(plan, blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
