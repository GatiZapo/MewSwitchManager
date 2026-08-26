using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

// Legacy analysis model kept for the older engine API. The dashboard uses
// MewSwitchManager.Models.SwitchExperienceSummary.
public sealed record LegacySwitchExperienceSummary(
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

    public async Task<LegacySwitchExperienceSummary> AnalyzeAsync(string targetRoot, CancellationToken ct = default)
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

        var hasHekate = File.Exists(Path.Combine(targetRoot, "bootloader", "update.bin"));
        var hasAtmosphere = File.Exists(Path.Combine(targetRoot, "atmosphere", "package3"));
        var hasNintendo = Directory.Exists(Path.Combine(targetRoot, "Nintendo"));
        var hasEmummc = Directory.Exists(Path.Combine(targetRoot, "emuMMC"));
        var hasConfig = File.Exists(Path.Combine(targetRoot, "bootloader", "hekate_ipl.ini"));
        var looksLikeSwitchSd = hasHekate || hasAtmosphere || hasNintendo || hasEmummc;

        var rootPath = Path.GetFullPath(targetRoot);
        var drive = new DriveInfo(Path.GetPathRoot(rootPath)!);
        var report = new SwitchSdReport(
            Root: rootPath,
            TotalBytes: drive.TotalSize,
            FreeBytes: drive.AvailableFreeSpace,
            LooksLikeSwitchSd: looksLikeSwitchSd,
            HasHekate: hasHekate,
            HasAtmosphere: hasAtmosphere,
            HasNintendo: hasNintendo,
            HasEmummc: hasEmummc,
            HasBootloaderConfig: hasConfig,
            Warnings: Array.Empty<string>());

        var recommendations = _checkpoints.Recommend(report);
        var ready = !warnings.Any(x => x.Contains("not detected", StringComparison.OrdinalIgnoreCase));
        var summary = ready ? "Switch storage looks ready for managed updates." : "Review the warnings before applying managed updates.";
        return new(targetRoot, healthy, warnings, recommendations, ready, summary);
    }
}
