using MewSwitchManager.Core;
using MewSwitchManager.Models;
using Xunit;

namespace MewSwitchManager.Tests;

public sealed class ComponentCatalogTests
{
    [Fact]
    public void CatalogRejectsUnknownDependency()
    {
        var json = """
        {
          "schemaVersion": 1,
          "generatedAt": "2026-08-27T00:00:00Z",
          "components": [
            { "id": "tesla", "name": "Tesla", "channel": "stable", "currentVersion": null, "latestVersion": "1.0.0", "supportedVersion": null, "dependencies": ["nx-ovlloader"], "conflicts": [], "releaseUrl": null, "sha256": null, "assetName": null }
          ]
        }
        """;
        Assert.Throws<InvalidDataException>(() => new ComponentCatalogService().Parse(json));
    }

    [Fact]
    public void CatalogPlanBlocksParentWhenDependencyIsIncompatible()
    {
        var catalog = new ComponentCatalog(1, DateTimeOffset.UtcNow, new[]
        {
            new ComponentCatalogEntry("nx-ovlloader", "nx-ovlloader", "stable", "1.0.0", "2.0.0", new VersionConstraint(Minimum: "2.0.0"), Array.Empty<string>(), Array.Empty<string>(), null, null, null),
            new ComponentCatalogEntry("tesla", "Tesla", "stable", null, "1.0.0", null, new[] { "nx-ovlloader" }, Array.Empty<string>(), null, null, null)
        });
        var installed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["nx-ovlloader"] = "1.0.0" };
        var plan = new ComponentCatalogService().BuildPlan(catalog, new[] { "tesla" }, installed);
        Assert.False(plan.CanApply);
        Assert.Contains("nx-ovlloader", plan.Incompatible);
        Assert.DoesNotContain(plan.Ordered, x => x.Id == "tesla");
    }
}
