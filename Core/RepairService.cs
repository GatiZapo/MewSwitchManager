using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed record RepairAction(string Id, bool Changed, string Message);

public sealed record RepairReport(DateTimeOffset CreatedAt, IReadOnlyList<RepairAction> Actions)
{
    public bool Changed => Actions.Any(x => x.Changed);
}

/// <summary>
/// Performs only local, reversible application repairs. It never formats storage,
/// rewrites Switch partitions, or silently modifies managed SD-card components.
/// </summary>
public sealed class RepairService
{
    private readonly AppPaths _paths;
    private readonly AppConfig _config;
    private readonly AppLogger _logger;

    public RepairService(AppPaths paths, AppConfig config, AppLogger logger)
    {
        _paths = paths;
        _config = config;
        _logger = logger;
    }

    public Task<RepairReport> RepairAsync(CancellationToken ct = default)
    {
        var actions = new List<RepairAction>();
        ct.ThrowIfCancellationRequested();

        actions.Add(EnsureDirectory(_paths.DataDirectory, "data"));
        actions.Add(EnsureDirectory(_paths.CacheDirectory, "cache"));
        actions.Add(RepairStateFile(_paths.StateFile, ct));
        actions.Add(RepairComponentState(Path.Combine(_paths.DataDirectory, "components.json"), ct));
        actions.Add(RepairLinuxImageCache(ct));
        actions.Add(CleanupStaleTransactions(ct));

        _logger.Info($"Safe repair completed: {actions.Count(x => x.Changed)} change(s), {actions.Count} action(s).");
        return Task.FromResult(new RepairReport(DateTimeOffset.UtcNow, actions));
    }

    private static RepairAction EnsureDirectory(string path, string id)
    {
        if (Directory.Exists(path)) return new(id, false, $"Directory is available: {path}");
        Directory.CreateDirectory(path);
        return new(id, true, $"Created missing directory: {path}");
    }

    private RepairAction RepairStateFile(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var store = new JsonStore<AppState>(path);
            var state = store.LoadOrCreate();
            state.EnsureStages();
            state.ReconcilePersistedProgress();
            var existed = File.Exists(path);
            store.Save(state);
            return new("state", !existed, existed ? "Persisted workflow state validated and normalized." : "Created missing workflow state.");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Workflow state repair failed: {ex.Message}");
            return new("state", false, "Workflow state could not be repaired automatically; the original file was left untouched.");
        }
    }

    private RepairAction RepairComponentState(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var store = new JsonStore<ComponentManagerState>(path);
            var state = store.LoadOrCreate();
            var existed = File.Exists(path);
            store.Save(state);
            return new("components", !existed, existed ? "Component state validated and normalized." : "Created missing component state.");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Component state repair failed: {ex.Message}");
            return new("components", false, "Component state could not be repaired automatically.");
        }
    }

    private RepairAction RepairLinuxImageCache(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var imagePath = new MewSwitchManager.Linux.LinuxImageService(new HttpClient(), _logger, _config).FinalPath(_paths.CacheDirectory);
            if (!File.Exists(imagePath)) return new("linux-image", false, "No cached Linux image requires repair.");
            if (new FileInfo(imagePath).Length > 0) return new("linux-image", false, "Cached Linux image is non-empty.");

            File.Delete(imagePath);
            _logger.Warn($"Removed zero-byte Linux image cache: {imagePath}");
            return new("linux-image", true, "Removed a zero-byte Linux image so the next download can resume cleanly.");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Linux image repair failed: {ex.Message}");
            return new("linux-image", false, "Linux image cache could not be repaired automatically.");
        }
    }

    private RepairAction CleanupStaleTransactions(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var transactionRoot = Path.Combine(_paths.CacheDirectory, "_mewnx-transactions");
        if (!Directory.Exists(transactionRoot)) return new("transactions", false, "No stale transaction directory exists.");

        var removed = 0;
        foreach (var directory in Directory.EnumerateDirectories(transactionRoot))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (Directory.GetLastWriteTimeUtc(directory) >= DateTime.UtcNow.AddDays(-1)) continue;
                Directory.Delete(directory, true);
                removed++;
            }
            catch (Exception ex) { _logger.Warn($"Could not remove stale transaction {directory}: {ex.Message}"); }
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(transactionRoot).Any()) Directory.Delete(transactionRoot, false);
        }
        catch { }

        return new("transactions", removed > 0, removed > 0 ? $"Removed {removed} stale transaction journal(s)." : "No stale transaction journals required cleanup.");
    }
}
