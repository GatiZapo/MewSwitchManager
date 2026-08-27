namespace MewSwitchManager.Core;

public sealed record UpdatePlanItem(string ComponentId, string CurrentVersion, string TargetVersion, IReadOnlyList<string> Dependencies);
public sealed record UpdatePlan(bool IsValid, IReadOnlyList<UpdatePlanItem> Items, IReadOnlyList<string> Errors)
{
    public static UpdatePlan Empty => new(true, Array.Empty<UpdatePlanItem>(), Array.Empty<string>());
}

/// <summary>Deterministic validation container for catalog-driven updates.</summary>
public sealed class UpdatePlanBuilder
{
    public UpdatePlan Build(IEnumerable<UpdatePlanItem> requested)
    {
        var items = requested.ToList();
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ComponentId)) errors.Add("Update contains an empty component id.");
            else if (!ids.Add(item.ComponentId)) errors.Add($"Duplicate component in update plan: {item.ComponentId}.");
            if (string.IsNullOrWhiteSpace(item.TargetVersion)) errors.Add($"Missing target version for {item.ComponentId}.");
            if (item.Dependencies.Any(string.IsNullOrWhiteSpace)) errors.Add($"Invalid dependency in {item.ComponentId}.");
        }
        return new(errors.Count == 0, items, errors);
    }
}
