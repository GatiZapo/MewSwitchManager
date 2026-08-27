namespace MewNX.Models;

public sealed record ComponentCatalogEntry(
    string Id,
    string Name,
    string Channel,
    string? CurrentVersion,
    string? LatestVersion,
    VersionConstraint? SupportedVersion,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Conflicts,
    string? ReleaseUrl,
    string? Sha256,
    string? AssetName);

public sealed record ComponentCatalog(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ComponentCatalogEntry> Components);

public sealed record ComponentUpdatePlan(
    IReadOnlyList<ComponentCatalogEntry> Ordered,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Incompatible,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> Cycles)
{
    public bool CanApply => Missing.Count == 0 && Incompatible.Count == 0 && Conflicts.Count == 0 && Cycles.Count == 0;
}
