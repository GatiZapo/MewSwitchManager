using MewSwitchManager.Core;
using MewSwitchManager.Models;

namespace MewSwitchManager.Tests;

public sealed class CatalogUpdateCoordinatorTests
{
    [Fact]
    public void BlocksMissingDependencies()
    {
        var catalog = new ComponentCatalog(1, DateTimeOffset.UtcNow, [
            new ComponentCatalogEntry("app", "App", "stable", null, "2.0", null, ["missing"], [], null, null, null)
        ]);
        var result = new CatalogUpdateCoordinator(new ComponentCatalogService()).Prepare(catalog, ["app"], new Dictionary<string,string>());
        Assert.False(result.CanProceed);
        Assert.Contains(result.Blockers, x => x.Contains("missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AllowsResolvablePlan()
    {
        var catalog = new ComponentCatalog(1, DateTimeOffset.UtcNow, [
            new ComponentCatalogEntry("dep", "Dependency", "stable", "1.0", "1.1", null, [], [], null, null, null),
            new ComponentCatalogEntry("app", "App", "stable", "1.0", "2.0", null, ["dep"], [], null, null, null)
        ]);
        var installed = new Dictionary<string,string> { ["dep"] = "1.0" };
        var result = new CatalogUpdateCoordinator(new ComponentCatalogService()).Prepare(catalog, ["app"], installed);
        Assert.True(result.CanProceed);
        Assert.Contains(result.Plan.Ordered, x => x.Id == "app");
    }
}
