using MewSwitchManager.Hardware;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public enum DiagnosticSeverity { Pass, Warning, Fail }
public sealed record DiagnosticCheck(string Id, string Title, DiagnosticSeverity Severity, string Message);
public sealed record DiagnosticReport(DateTimeOffset CreatedAt, IReadOnlyList<DiagnosticCheck> Checks) { public bool HasFailures => Checks.Any(x => x.Severity == DiagnosticSeverity.Fail); public bool HasWarnings => Checks.Any(x => x.Severity == DiagnosticSeverity.Warning); }

public sealed class SystemDiagnostics
{
    private readonly AppPaths _paths; private readonly AppConfig _config; private readonly AppLogger _logger;
    public SystemDiagnostics(AppPaths paths, AppConfig config, AppLogger logger) { _paths = paths; _config = config; _logger = logger; }
    public async Task<DiagnosticReport> RunAsync(InstallationEngine engine, CancellationToken ct = default)
    {
        var checks = new List<DiagnosticCheck>(); ct.ThrowIfCancellationRequested();
        checks.Add(OperatingSystem.IsWindows() ? new("platform", "Windows", DiagnosticSeverity.Pass, "Windows is supported.") : new("platform", "Windows", DiagnosticSeverity.Fail, "MewNX requires Windows."));
        checks.Add(Directory.Exists(_paths.DataDirectory) ? new("data", "Application data", DiagnosticSeverity.Pass, _paths.DataDirectory) : new("data", "Application data", DiagnosticSeverity.Fail, "Application data directory is unavailable."));
        var freeBytes = 0L; try { var root = Path.GetPathRoot(_paths.CacheDirectory); if (!string.IsNullOrWhiteSpace(root)) freeBytes = new DriveInfo(root).AvailableFreeSpace; } catch (Exception ex) { _logger.Warn($"Diagnostics disk-space probe failed: {ex.Message}"); }
        const long recommendedFree = 10L * 1024 * 1024 * 1024; checks.Add(freeBytes >= recommendedFree ? new("space", "Working storage", DiagnosticSeverity.Pass, $"{freeBytes / 1024d / 1024d / 1024d:F1} GiB available; recommended minimum is 10 GiB.") : new("space", "Working storage", freeBytes > 0 ? DiagnosticSeverity.Warning : DiagnosticSeverity.Fail, freeBytes > 0 ? $"Only {freeBytes / 1024d / 1024d / 1024d:F1} GiB is available; downloads/extraction may fail." : "Could not determine available working storage."));
        checks.Add(engine.WslReady ? new("wsl", "WSL", DiagnosticSeverity.Pass, "WSL is available.") : new("wsl", "WSL", DiagnosticSeverity.Warning, "WSL was not detected as ready."));
        checks.Add(engine.RcmConnected ? new("rcm", "RCM device", DiagnosticSeverity.Pass, "Nintendo Switch RCM/APX device detected.") : new("rcm", "RCM device", DiagnosticSeverity.Warning, "RCM/APX device is not currently connected. Check USB cable/port and driver when the Switch is in RCM."));
        var catalogPath = Path.Combine(AppContext.BaseDirectory, "Models", "MewNxManifest.json"); if (!File.Exists(catalogPath)) catalogPath = Path.Combine(AppContext.BaseDirectory, "MewNxManifest.json");
        try { var catalog = new ComponentCatalogService().Load(catalogPath); checks.Add(new("catalog", "Component catalog", DiagnosticSeverity.Pass, $"Schema {catalog.SchemaVersion} • {catalog.Components.Count} components • dependency references valid.")); } catch (Exception ex) { checks.Add(new("catalog", "Component catalog", DiagnosticSeverity.Fail, ex.Message)); }
        var drives = new RemovableDriveService().Scan(); var sd = drives.Select(d => SafeInspect(d.Root)).FirstOrDefault(x => x?.LooksLikeSwitchSd == true);
        if (sd is null) checks.Add(new("sd", "Switch SD", DiagnosticSeverity.Warning, "No Switch-like removable storage is currently mounted.")); else
        {
            checks.Add(new("sd", "Switch SD", DiagnosticSeverity.Pass, $"{sd.Root} • {sd.TotalBytes / 1024d / 1024d / 1024d:F1} GiB total • {sd.FreeBytes / 1024d / 1024d / 1024d:F1} GiB free."));
            checks.Add(sd.HasHekate && sd.HasBootloaderConfig ? new("sd-boot", "Hekate files", DiagnosticSeverity.Pass, "bootloader/update.bin and hekate_ipl.ini detected.") : new("sd-boot", "Hekate files", DiagnosticSeverity.Warning, "Hekate boot files are incomplete or not detected."));
            checks.Add(sd.HasAtmosphere ? new("sd-atmosphere", "Atmosphère", DiagnosticSeverity.Pass, "atmosphere/package3 detected.") : new("sd-atmosphere", "Atmosphère", DiagnosticSeverity.Warning, "atmosphere/package3 not detected."));
            checks.Add(sd.HasEmummc ? new("sd-emummc", "emuMMC", DiagnosticSeverity.Pass, "emuMMC directory detected.") : new("sd-emummc", "emuMMC", DiagnosticSeverity.Warning, "emuMMC directory not detected."));
            foreach (var warning in sd.Warnings) checks.Add(new("sd-warning-" + checks.Count, "SD warning", DiagnosticSeverity.Warning, warning));
            try
            {
                var deep = await new ComponentHealthService().ScanAsync(sd.Root, null, ct);
                foreach (var item in deep) { var severity = item.Severity switch { ComponentHealthSeverity.Healthy => DiagnosticSeverity.Pass, ComponentHealthSeverity.Warning => DiagnosticSeverity.Warning, _ => DiagnosticSeverity.Fail }; var detail = item.ActualSha256 is null ? item.Message : $"{item.Message} SHA-256={item.ActualSha256}"; checks.Add(new("component-health-" + item.ComponentId, $"Component health: {item.Title}", severity, detail)); }
            }
            catch (Exception ex) { _logger.Warn($"Deep component health scan failed: {ex.Message}"); checks.Add(new("component-health", "Component health", DiagnosticSeverity.Warning, "Deep component integrity scan could not be completed.")); }
        }
        checks.Add(engine.HekateDetected ? new("hekate", "Hekate configuration", DiagnosticSeverity.Pass, "Hekate configuration detected on a mounted volume.") : new("hekate", "Hekate configuration", DiagnosticSeverity.Warning, "Hekate configuration was not detected on mounted volumes."));
        var imagePath = new MewSwitchManager.Linux.LinuxImageService(new HttpClient(), _logger, _config).FinalPath(_paths.CacheDirectory); if (!File.Exists(imagePath)) checks.Add(new("linux-image", "Linux image", DiagnosticSeverity.Warning, "Linux image is not present in the local cache.")); else if (new FileInfo(imagePath).Length == 0) checks.Add(new("linux-image", "Linux image", DiagnosticSeverity.Fail, "Linux image exists but is empty.")); else checks.Add(new("linux-image", "Linux image", DiagnosticSeverity.Pass, $"Cached image: {new FileInfo(imagePath).Length / 1024d / 1024d:F1} MiB."));
        var state = engine.State; checks.Add(state.LinuxVerified ? new("state", "Installation state", DiagnosticSeverity.Pass, $"Progress persisted at {state.CurrentStage}; the image will be revalidated before destructive use.") : new("state", "Installation state", DiagnosticSeverity.Warning, $"No verified Linux image is currently persisted; stage is {state.CurrentStage}."));
        var report = new DiagnosticReport(DateTimeOffset.UtcNow, checks); _logger.Info($"Diagnostics completed: {checks.Count(x => x.Severity == DiagnosticSeverity.Pass)} pass, {checks.Count(x => x.Severity == DiagnosticSeverity.Warning)} warning, {checks.Count(x => x.Severity == DiagnosticSeverity.Fail)} fail."); return report;
    }
    private static SwitchSdReport? SafeInspect(string root) { try { return new SwitchSdInspector().Inspect(root); } catch { return null; } }
}
