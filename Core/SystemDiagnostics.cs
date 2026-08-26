using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public enum DiagnosticSeverity { Pass, Warning, Fail }

public sealed record DiagnosticCheck(string Id, string Title, DiagnosticSeverity Severity, string Message);

public sealed record DiagnosticReport(DateTimeOffset CreatedAt, IReadOnlyList<DiagnosticCheck> Checks)
{
    public bool HasFailures => Checks.Any(x => x.Severity == DiagnosticSeverity.Fail);
    public bool HasWarnings => Checks.Any(x => x.Severity == DiagnosticSeverity.Warning);
}

public sealed class SystemDiagnostics
{
    private readonly AppPaths _paths;
    private readonly AppConfig _config;
    private readonly AppLogger _logger;

    public SystemDiagnostics(AppPaths paths, AppConfig config, AppLogger logger)
    {
        _paths = paths;
        _config = config;
        _logger = logger;
    }

    public Task<DiagnosticReport> RunAsync(InstallationEngine engine, CancellationToken ct = default)
    {
        var checks = new List<DiagnosticCheck>();
        ct.ThrowIfCancellationRequested();

        checks.Add(OperatingSystem.IsWindows()
            ? new("platform", "Windows", DiagnosticSeverity.Pass, "Windows is supported.")
            : new("platform", "Windows", DiagnosticSeverity.Fail, "MewNX requires Windows."));

        checks.Add(Directory.Exists(_paths.DataDirectory)
            ? new("data", "Application data", DiagnosticSeverity.Pass, _paths.DataDirectory)
            : new("data", "Application data", DiagnosticSeverity.Fail, "Application data directory is unavailable."));

        var freeBytes = 0L;
        try
        {
            var root = Path.GetPathRoot(_paths.CacheDirectory);
            if (!string.IsNullOrWhiteSpace(root)) freeBytes = new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) { _logger.Warn($"Diagnostics disk-space probe failed: {ex.Message}"); }

        checks.Add(freeBytes >= 2L * 1024 * 1024 * 1024
            ? new("space", "Cache storage", DiagnosticSeverity.Pass, $"{freeBytes / 1024d / 1024d / 1024d:F1} GiB available.")
            : new("space", "Cache storage", DiagnosticSeverity.Warning, "Less than 2 GiB is available on the cache volume."));

        checks.Add(engine.WslReady
            ? new("wsl", "WSL", DiagnosticSeverity.Pass, "WSL is available.")
            : new("wsl", "WSL", DiagnosticSeverity.Warning, "WSL was not detected as ready."));

        checks.Add(engine.RcmConnected
            ? new("rcm", "RCM device", DiagnosticSeverity.Pass, "Nintendo Switch RCM device detected.")
            : new("rcm", "RCM device", DiagnosticSeverity.Warning, "RCM device is not currently connected."));

        checks.Add(engine.HekateDetected
            ? new("hekate", "Hekate / SD", DiagnosticSeverity.Pass, "Hekate configuration detected on a mounted volume.")
            : new("hekate", "Hekate / SD", DiagnosticSeverity.Warning, "Hekate configuration was not detected on mounted volumes."));

        var imagePath = new MewSwitchManager.Linux.LinuxImageService(new HttpClient(), _logger, _config).FinalPath(_paths.CacheDirectory);
        if (!File.Exists(imagePath)) checks.Add(new("linux-image", "Linux image", DiagnosticSeverity.Warning, "Linux image is not present in the local cache."));
        else if (new FileInfo(imagePath).Length == 0) checks.Add(new("linux-image", "Linux image", DiagnosticSeverity.Fail, "Linux image exists but is empty."));
        else checks.Add(new("linux-image", "Linux image", DiagnosticSeverity.Pass, $"Cached image: {new FileInfo(imagePath).Length / 1024d / 1024d:F1} MiB."));

        var state = engine.State;
        checks.Add(state.LinuxVerified
            ? new("state", "Installation state", DiagnosticSeverity.Pass, $"Progress persisted at {state.CurrentStage}.")
            : new("state", "Installation state", DiagnosticSeverity.Warning, $"No verified Linux image is currently persisted; stage is {state.CurrentStage}."));

        var report = new DiagnosticReport(DateTimeOffset.UtcNow, checks);
        _logger.Info($"Diagnostics completed: {checks.Count(x => x.Severity == DiagnosticSeverity.Pass)} pass, {checks.Count(x => x.Severity == DiagnosticSeverity.Warning)} warning, {checks.Count(x => x.Severity == DiagnosticSeverity.Fail)} fail.");
        return Task.FromResult(report);
    }
}
