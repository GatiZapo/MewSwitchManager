namespace MewNX.Core;

public sealed record HealthIssue(string Code, string Path, string Message, bool Repairable);
public sealed record HealthReport(bool Healthy, IReadOnlyList<HealthIssue> Issues);

/// <summary>Safe filesystem health checks and conservative repairs for managed paths.</summary>
public sealed class HealthRepairService
{
    private readonly StateReconciliationService _reconciler = new();

    public async Task<HealthReport> CheckAsync(
        string root,
        IReadOnlyDictionary<string, string> expectedSha256,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(expectedSha256);

        if (!Directory.Exists(root))
            return new(false, [new("ROOT_UNAVAILABLE", root, "Target directory is unavailable.", false)]);

        var result = await _reconciler.ReconcileHashesAsync(expectedSha256, root, ct).ConfigureAwait(false);
        var issues = new List<HealthIssue>(result.MissingPaths.Count + result.InvalidPaths.Count);
        issues.AddRange(result.MissingPaths.Select(static p =>
            new HealthIssue("MISSING", p, "Expected file is missing.", true)));
        issues.AddRange(result.InvalidPaths.Select(static p =>
            new HealthIssue("INTEGRITY", p, "File failed integrity verification.", true)));
        return new(issues.Count == 0, issues);
    }

    public async Task<HealthReport> RepairAsync(
        string root,
        IReadOnlyDictionary<string, string> expectedSha256,
        Func<string, CancellationToken, Task<bool>> restoreAsync,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(expectedSha256);
        ArgumentNullException.ThrowIfNull(restoreAsync);

        var before = await CheckAsync(root, expectedSha256, ct).ConfigureAwait(false);
        foreach (var issue in before.Issues.Where(static x => x.Repairable))
        {
            ct.ThrowIfCancellationRequested();
            if (!await restoreAsync(issue.Path, ct).ConfigureAwait(false))
                break;
        }

        return await CheckAsync(root, expectedSha256, ct).ConfigureAwait(false);
    }
}
