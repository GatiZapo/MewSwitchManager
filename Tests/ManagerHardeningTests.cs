using MewNX.Core;
using MewNX.Models;
using Xunit;

namespace MewSwitchManager.Tests;

public sealed class ManagerHardeningTests
{
    [Fact]
    public void CatalogPlanIncludesOutdatedInstalledComponent()
    {
        var catalog = new ComponentCatalog(1, DateTimeOffset.UtcNow, [
            new ComponentCatalogEntry("atmosphere", "Atmosphère", "stable", "1.0.0", "1.1.0", null, [], [], null, null, null)
        ]);
        var plan = new ComponentCatalogService().BuildPlan(catalog, ["atmosphere"],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["atmosphere"] = "1.0.0" });
        Assert.True(plan.CanApply);
        Assert.Single(plan.Ordered);
        Assert.Equal("atmosphere", plan.Ordered[0].Id);
    }

    [Fact]
    public void CatalogPlanDoesNotDowngradeNewerInstalledComponent()
    {
        var catalog = new ComponentCatalog(1, DateTimeOffset.UtcNow, [
            new ComponentCatalogEntry("atmosphere", "Atmosphère", "stable", "1.0.0", "1.1.0", null, [], [], null, null)
        ]);
        var plan = new ComponentCatalogService().BuildPlan(catalog, ["atmosphere"],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["atmosphere"] = "1.2.0" });
        Assert.True(plan.CanApply);
        Assert.Empty(plan.Ordered);
    }

    [Fact]
    public void CatalogRejectsConflictReferenceToUnknownComponent()
    {
        var json = """
        {
          "schemaVersion": 1,
          "generatedAt": "2026-08-27T00:00:00Z",
          "components": [
            { "id": "tesla", "name": "Tesla", "channel": "stable", "currentVersion": null, "latestVersion": null, "supportedVersion": null, "dependencies": [], "conflicts": ["missing"], "releaseUrl": null, "sha256": null, "assetName": null }
          ]
        }
        """;
        Assert.Throws<InvalidDataException>(() => new ComponentCatalogService().Parse(json));
    }

    [Fact]
    public void CatalogPlanBlocksRequestedComponentWhenInstalledConflictExists()
    {
        var catalog = new ComponentCatalog(1, DateTimeOffset.UtcNow, [
            new ComponentCatalogEntry("a", "A", "stable", null, "1.0.0", null, [], ["b"], null, null, null),
            new ComponentCatalogEntry("b", "B", "stable", null, "1.0.0", null, [], [], null, null, null)
        ]);
        var plan = new ComponentCatalogService().BuildPlan(catalog, ["a"],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["b"] = "1.0.0" });
        Assert.False(plan.CanApply);
        Assert.Contains("a:b", plan.Conflicts);
        Assert.Empty(plan.Ordered);
    }

    [Fact]
    public void TransactionRollbackRestoresExactDirectoryContents()
    {
        var root = Path.Combine(Path.GetTempPath(), "MewNX-tests", Guid.NewGuid().ToString("N"));
        var component = Path.Combine(root, "component");
        Directory.CreateDirectory(component);
        File.WriteAllText(Path.Combine(component, "existing.txt"), "before");
        try
        {
            using var transaction = new TransactionalRollback(root);
            transaction.CaptureDirectory(component);
            File.WriteAllText(Path.Combine(component, "existing.txt"), "after");
            File.WriteAllText(Path.Combine(component, "new.txt"), "created");
            Directory.CreateDirectory(Path.Combine(component, "new-folder"));
            File.WriteAllText(Path.Combine(component, "new-folder", "nested.txt"), "nested");
            transaction.Rollback();
            Assert.True(transaction.VerifyRestoredState());
            Assert.Equal("before", File.ReadAllText(Path.Combine(component, "existing.txt")));
            Assert.False(File.Exists(Path.Combine(component, "new.txt")));
            Assert.False(Directory.Exists(Path.Combine(component, "new-folder")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
