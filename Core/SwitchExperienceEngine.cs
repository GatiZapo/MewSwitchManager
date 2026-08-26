using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed record SwitchExperienceSummary(
    string TargetRoot,
    IReadOnlyList<string> Healthy,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ToolRecommendation> Recommendations,
    bool ReadyForUpdate,
    string Summary);

public sealed class SwitchExperienceEngine
{
    private readonly SwitchComponentManager _components;
    private readonly SwitchCheckpoint _checkpoints;

    public SwitchExperienceEngine(SwitchComponentManager components, SwitchCheckpoint checkpoints)
    {
        _components = components;
        _checkpoints = checkpoints;
    }

    public async Task<SwitchExperienceSummary> AnalyzeAsync(string targetRoot, CancellationToken ct = default)
    {
        var healthy = new List<string>();
        var warnings = new List<string>();
        var statuses = await _components.ScanAsync(targetRoot, ct);

        foreach (var status in statuses)
        {
            if (status.Installed) healthy.Add($"{status.Definition.Name}: {status.InstalledVersion}");
            else warnings.Add($"{status.Definition.Name}: not detected");
            if (status.UpdateAvailable) warnings.Add($"{status.Definition.Name}: update available ({status.AvailableVersion})");
        }

        var report = new SwitchSdReport(
            File.Exists(Path.Combine(targetRoot, "bootloader", "update.bin")),
            File.Exists(Path.Combine(targetRoot, "atmosphere", "package3")),
            Directory.Exists(Path.Combine(targetRoot, "Nintendo")),
            File.Exists(Path.Combine(targetRoot, "emuMMC", "emummc.ini")),
            Array.Empty<string>());
        var recommendations = _checkpoints.Recommend(report);
        var ready = !warnings.Any(x => x.Contains("not detected", StringComparison.OrdinalIgnoreCase));
        var summary = ready ? "Switch storage looks ready for managed updates." : "Review the warnings before applying managed updates.";
        return new(targetRoot, healthy, warnings, recommendations, ready, summary);
    }
}
