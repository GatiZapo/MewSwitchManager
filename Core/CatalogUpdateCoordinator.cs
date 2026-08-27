using MewNX.Models;

namespace MewNX.Core;

public sealed record CatalogUpdateSelection(
    ComponentUpdatePlan Plan,
    IReadOnlyList<string> Blockers)
{
    public bool CanProceed => Plan.CanApply && Blockers.Count == 0;
}

/// <summary>Single gate between catalog planning and transactional execution.</summary>
public sealed class CatalogUpdateCoordinator
{
    private readonly ComponentCatalogService _catalog;

    public CatalogUpdateCoordinator(ComponentCatalogService catalog)
        => _catalog = catalog;

    public CatalogUpdateSelection Prepare(
        ComponentCatalog catalog,
        IEnumerable<string> requested,
        IReadOnlyDictionary<string, string> installedVersions)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(installedVersions);

        var plan = _catalog.BuildPlan(catalog, requested, installedVersions);
        var blockers = BuildBlockers(plan);
        return new(plan, blockers);
    }

    private static IReadOnlyList<string> BuildBlockers(ComponentUpdatePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return [
            ..plan.Missing.Select(static value => $"Missing component or dependency: {value}"),
            ..plan.Incompatible.Select(static value => $"Incompatible installed version: {value}"),
            ..plan.Conflicts.Select(static value => $"Component conflict: {value}"),
            ..plan.Cycles.Select(static value => $"Dependency cycle: {value}")
        ]
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }
}
