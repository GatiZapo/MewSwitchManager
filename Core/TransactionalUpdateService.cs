namespace MewNX.Core;

public sealed record TransactionalUpdateResult(
    bool Success,
    bool RolledBack,
    bool RollbackVerified,
    string? Error);

/// <summary>Executes an already validated update as one journaled transaction.</summary>
public sealed class TransactionalUpdateService
{
    private readonly OperationJournal _journal;

    public TransactionalUpdateService(OperationJournal journal)
        => _journal = journal;

    public async Task<TransactionalUpdateResult> ExecuteAsync(
        string operationId,
        IEnumerable<string> targets,
        string workingDirectory,
        Func<CancellationToken, Task> applyAsync,
        Func<CancellationToken, Task<bool>> verifyAsync,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(applyAsync);
        ArgumentNullException.ThrowIfNull(verifyAsync);

        var targetList = targets
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var transaction = new TransactionalRollback(workingDirectory);
        foreach (var target in targetList)
        {
            if (File.Exists(target))
                transaction.Capture(target);
            else if (Directory.Exists(target))
                transaction.CaptureDirectory(target);
        }

        _journal.Append(new(operationId, "update", "Started", DateTimeOffset.UtcNow));

        try
        {
            _journal.Append(new(operationId, "update", "Staging", DateTimeOffset.UtcNow));
            await applyAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            _journal.Append(new(operationId, "update", "Verifying", DateTimeOffset.UtcNow));
            if (!await verifyAsync(ct).ConfigureAwait(false))
                throw new InvalidDataException("Post-update verification failed.");

            transaction.Commit();
            _journal.Append(new(operationId, "update", "Completed", DateTimeOffset.UtcNow));
            return new(true, false, true, null);
        }
        catch (OperationCanceledException ex)
        {
            return Rollback(operationId, transaction, ex);
        }
        catch (Exception ex)
        {
            return Rollback(operationId, transaction, ex);
        }
    }

    private TransactionalUpdateResult Rollback(
        string operationId,
        TransactionalRollback transaction,
        Exception originalError)
    {
        try
        {
            transaction.Rollback();
            var verified = transaction.VerifyRestoredState();
            _journal.Append(new(
                operationId,
                "update",
                verified ? "RolledBack" : "RollbackFailed",
                DateTimeOffset.UtcNow,
                originalError.Message));
            return new(false, true, verified, originalError.Message);
        }
        catch (Exception rollbackError)
        {
            _journal.Append(new(
                operationId,
                "update",
                "RollbackFailed",
                DateTimeOffset.UtcNow,
                rollbackError.Message));
            return new(false, true, false,
                $"{originalError.Message} Rollback failed: {rollbackError.Message}");
        }
    }
}
