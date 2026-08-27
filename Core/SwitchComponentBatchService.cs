using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

/// <summary>
/// Applies a selected set of Switch component updates under one outer transaction.
/// Individual component updates keep their own safety checks; the outer transaction
/// guarantees that a later failure restores components that were already updated.
/// </summary>
public sealed class SwitchComponentBatchService
{
    private readonly string _workspace;

    public SwitchComponentBatchService(string workspace)
    {
        _workspace = workspace;
        Directory.CreateDirectory(workspace);
    }

    public async Task<IReadOnlyList<ComponentStatus>> ApplyAsync(
        SwitchComponentManager manager,
        IEnumerable<SwitchComponent> components,
        string targetRoot,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var selected = components.Distinct().ToArray();
        if (selected.Length == 0) return Array.Empty<ComponentStatus>();
        if (!Directory.Exists(targetRoot)) throw new DirectoryNotFoundException(targetRoot);

        var managedRoots = selected.Select(component => component switch
        {
            SwitchComponent.Hekate => Path.Combine(targetRoot, "bootloader"),
            SwitchComponent.Atmosphere => Path.Combine(targetRoot, "atmosphere"),
            SwitchComponent.Dbi => Path.Combine(targetRoot, "switch", "DBI"),
            _ => Path.Combine(targetRoot, "switch")
        }).Append(Path.Combine(targetRoot, "MewNX", "state")).ToArray();

        var results = new List<ComponentStatus>(selected.Length);
        var coordinator = new TransactionalUpdateCoordinator(_workspace);
        await coordinator.ExecuteAsync(managedRoots, async token =>
        {
            foreach (var component in selected)
            {
                token.ThrowIfCancellationRequested();
                var result = await manager.InstallOrUpdateAsync(component, targetRoot, progress, token).ConfigureAwait(false);
                results.Add(result);
            }
        }, ct).ConfigureAwait(false);

        return results;
    }
}
