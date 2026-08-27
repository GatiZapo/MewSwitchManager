namespace MewSwitchManager.Core;

public sealed record TransactionalUpdateResult(bool Success, bool RolledBack, bool RollbackVerified, string? Error);

/// <summary>Coordinates a planned update as one journaled filesystem transaction.</summary>
public sealed class TransactionalUpdateService
{
    private readonly OperationJournal _journal;
    public TransactionalUpdateService(OperationJournal journal) => _journal = journal;

    public async Task<TransactionalUpdateResult> ExecuteAsync(
        string operationId,
        IEnumerable<string> targets,
        string workingDirectory,
        Func<CancellationToken, Task> applyAsync,
        Func<CancellationToken, Task<bool>> verifyAsync,
        CancellationToken ct = default)
    {
        using var tx = new TransactionalRollback(workingDirectory);
        foreach (var target in targets.Where(File.Exists)) tx.Capture(target);
        foreach (var target in targets.Where(Directory.Exists)) tx.CaptureDirectory(target);
        _journal.Append(new(operationId, "update", "Started", DateTimeOffset.UtcNow));
        try
        {
            _journal.Append(new(operationId, "update", "Staging", DateTimeOffset.UtcNow));
            await applyAsync(ct);
            ct.ThrowIfCancellationRequested();
            _journal.Append(new(operationId, "update", "Verifying", DateTimeOffset.UtcNow));
            if (!await verifyAsync(ct)) throw new InvalidDataException("Post-update verification failed.");
            tx.Commit();
            _journal.Append(new(operationId, "update", "Completed", DateTimeOffset.UtcNow));
            return new(true, false, true, null);
        }
        catch (Exception ex)
        {
            try
            {
                tx.Rollback();
                var verified = tx.VerifyRestoredState();
                _journal.Append(new(operationId, "update", verified ? "RolledBack" : "RollbackFailed", DateTimeOffset.UtcNow, ex.Message));
                return new(false, true, verified, ex.Message);
            }
            catch (Exception rollbackEx)
            {
                _journal.Append(new(operationId, "update", "RollbackFailed", DateTimeOffset.UtcNow, rollbackEx.Message));
                return new(false, true, false, $"{ex.Message} Rollback failed: {rollbackEx.Message}");
            }
        }
    }
}
