using MewSwitchManager.Core;
using MewSwitchManager.Models;
namespace MewSwitchManager.Tests;
public sealed class CatalogTransactionalUpdateServiceTests
{
    [Fact]
    public async Task BlockedCatalogNeverExecutesApply()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var catalog = new ComponentCatalog(1, DateTimeOffset.UtcNow, [new("app", "App", "stable", null, "2", null, ["missing"], [], null, null, null)]);
            var service = new CatalogTransactionalUpdateService(new CatalogUpdateCoordinator(new ComponentCatalogService()), new TransactionalUpdateService(new OperationJournal(Path.Combine(root, "journal.json"))));
            var applied = false;
            var result = await service.ExecuteAsync(catalog, ["app"], new Dictionary<string,string>(), "op", [], root, _ => { applied = true; return Task.CompletedTask; }, _ => Task.FromResult(true));
            Assert.False(result.Success); Assert.False(result.PlanAccepted); Assert.False(applied);
        }
        finally { Directory.Delete(root, true); }
    }
}
