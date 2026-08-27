namespace MewSwitchManager.Core;

/// <summary>
/// Coordinates multi-root updates as one transaction. Every managed root is captured
/// before mutation; a failure rolls all roots back and the restored state is verified.
/// </summary>
public sealed class TransactionalUpdateCoordinator
{
    private readonly string _transactionWorkspace;

    public TransactionalUpdateCoordinator(string transactionWorkspace)
    {
        _transactionWorkspace = transactionWorkspace;
        Directory.CreateDirectory(transactionWorkspace);
    }

    public async Task ExecuteAsync(
        IEnumerable<string> managedRoots,
        Func<CancellationToken, Task> apply,
        CancellationToken ct = default)
    {
        var roots = managedRoots
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var transaction = new TransactionalRollback(_transactionWorkspace);
        foreach (var root in roots)
        {
            ct.ThrowIfCancellationRequested();
            transaction.CaptureDirectory(root);
        }

        try
        {
            await apply(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            transaction.Commit();
        }
        catch (Exception operationError)
        {
            transaction.Rollback();
            if (!transaction.VerifyRestoredState())
                throw new AggregateException("Update failed and transactional rollback could not be verified.", operationError,
                    new IOException("The managed filesystem does not match the pre-update snapshot."));
            throw;
        }
    }
}
