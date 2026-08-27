using MewSwitchManager.Core;
using MewSwitchManager.Models;

namespace MewSwitchManager.Tests;

public sealed class ComponentCatalogServiceTests
{
    [Fact]
    public void BuildsDependencyOrderAndSkipsInstalledComponents()
    {
        var catalog = new ComponentCatalog(1, DateTimeOffset.UtcNow,
        [
            new ComponentCatalogEntry("core", "Core", "stable", null, "1.0.0", null, [], [], null, null, null),
            new ComponentCatalogEntry("ui", "UI", "stable", null, "1.0.0", null, ["core"], [], null, null, null)
        ]);

        var plan = new ComponentCatalogService().BuildPlan(catalog, ["ui"],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["core"] = "1.0.0" });

        Assert.True(plan.CanApply);
        Assert.Single(plan.Ordered);
        Assert.Equal("ui", plan.Ordered[0].Id);
    }

    [Fact]
    public void ReportsMissingDependenciesCyclesAndConflicts()
    {
        var catalog = new ComponentCatalog(1, DateTimeOffset.UtcNow,
        [
            new ComponentCatalogEntry("a", "A", "stable", null, null, null, ["missing", "b"], ["conflict"], null, null, null),
            new ComponentCatalogEntry("b", "B", "stable", null, null, null, ["a"], [], null, null, null),
            new ComponentCatalogEntry("conflict", "Conflict", "stable", "1.0.0", "1.0.0", null, [], [], null, null, null)
        ]);

        var installed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["conflict"] = "1.0.0" };
        var plan = new ComponentCatalogService().BuildPlan(catalog, ["a"], installed);

        Assert.Contains("missing", plan.Missing);
        Assert.Contains("a", plan.Cycles);
        Assert.Contains("a:conflict", plan.Conflicts);
        Assert.False(plan.CanApply);
    }

    [Fact]
    public void RejectsDuplicateIds()
    {
        var json = "{\"schemaVersion\":1,\"generatedAt\":\"2026-08-27T00:00:00Z\",\"components\":[" +
                   "{\"id\":\"x\",\"name\":\"X\",\"channel\":\"stable\",\"dependencies\":[],\"conflicts\":[]}," +
                   "{\"id\":\"x\",\"name\":\"X2\",\"channel\":\"stable\",\"dependencies\":[],\"conflicts\":[]}] }";

        Assert.Throws<InvalidDataException>(() => new ComponentCatalogService().Parse(json));
    }
}
