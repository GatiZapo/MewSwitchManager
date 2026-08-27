namespace MewSwitchManager.Core;

public sealed record HealthIssue(string Code, string Path, string Message, bool Repairable);
public sealed record HealthReport(bool Healthy, IReadOnlyList<HealthIssue> Issues);

/// <summary>Safe filesystem health checks and conservative repairs for managed paths.</summary>
public sealed class HealthRepairService
{
    private readonly StateReconciliationService _reconciler = new();

    public async Task<HealthReport> CheckAsync(string root, IReadOnlyDictionary<string,string> expectedSha256, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return new(false, [new("ROOT_UNAVAILABLE", root ?? string.Empty, "Target directory is unavailable.", false)]);
        var result = await _reconciler.ReconcileHashesAsync(expectedSha256, root, ct);
        var issues = new List<HealthIssue>();
        issues.AddRange(result.MissingPaths.Select(p => new HealthIssue("MISSING", p, "Expected file is missing.", true)));
        issues.AddRange(result.InvalidPaths.Select(p => new HealthIssue("INTEGRITY", p, "File failed integrity verification.", true)));
        return new(issues.Count == 0, issues);
    }

    public async Task<HealthReport> RepairAsync(string root, IReadOnlyDictionary<string,string> expectedSha256, Func<string,CancellationToken,Task<bool>> restoreAsync, CancellationToken ct = default)
    {
        var before = await CheckAsync(root, expectedSha256, ct);
        foreach (var issue in before.Issues.Where(x => x.Repairable))
        {
            ct.ThrowIfCancellationRequested();
            if (!await restoreAsync(issue.Path, ct))
                return await CheckAsync(root, expectedSha256, ct);
        }
        return await CheckAsync(root, expectedSha256, ct);
    }
}
