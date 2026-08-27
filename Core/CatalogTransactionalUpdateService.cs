using MewSwitchManager.Models;
namespace MewSwitchManager.Core;
public sealed record CatalogTransactionalUpdateResult(bool Success, bool PlanAccepted, bool RolledBack, bool RollbackVerified, IReadOnlyList<string> Blockers, string? Error);
/// <summary>End-to-end gate from catalog state to one transactional update.</summary>
public sealed class CatalogTransactionalUpdateService
{
    private readonly CatalogUpdateCoordinator _coordinator;
    private readonly TransactionalUpdateService _transaction;
    public CatalogTransactionalUpdateService(CatalogUpdateCoordinator coordinator, TransactionalUpdateService transaction) { _coordinator = coordinator; _transaction = transaction; }
    public async Task<CatalogTransactionalUpdateResult> ExecuteAsync(ComponentCatalog catalog, IEnumerable<string> requested, IReadOnlyDictionary<string, string> installedVersions, string operationId, IEnumerable<string> targets, string workingDirectory, Func<CancellationToken, Task> applyAsync, Func<CancellationToken, Task<bool>> verifyAsync, CancellationToken ct = default)
    {
        var selection = _coordinator.Prepare(catalog, requested, installedVersions);
        if (!selection.CanProceed) return new(false, false, false, false, selection.Blockers, "Update plan was blocked by catalog constraints.");
        var result = await _transaction.ExecuteAsync(operationId, targets, workingDirectory, applyAsync, verifyAsync, ct);
        return new(result.Success, true, result.RolledBack, result.RollbackVerified, Array.Empty<string>(), result.Error);
    }
}
