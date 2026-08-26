using MewSwitchManager.Hardware;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Linux;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed class InstallationEngine
{
    private readonly AppState _state;
    private readonly JsonStore<AppState> _store;
    private readonly AppLogger _logger;
    private readonly DiskService _disks;
    private readonly SystemProbe _probe;
    private readonly LinuxImageService _linux;
    private readonly UsbStorageService _usb;
    private readonly DependencyService _dependencies;
    private readonly SafetyEngine _safety;
    private readonly string _cache;
    private readonly AppConfig _config;

    public AppState State => _state;
    public IReadOnlyList<DiskInfo> Disks { get; private set; } = [];
    public bool WslReady { get; private set; }
    public bool RcmConnected { get; private set; }
    public bool HekateDetected { get; private set; }
    public event Action? StateChanged;

    public InstallationEngine(AppPaths paths, AppConfig config, AppLogger logger)
    {
        _state = new JsonStore<AppState>(paths.StateFile).LoadOrCreate();
        _state.EnsureStages();
        _state.ReconcilePersistedProgress();
        _store = new JsonStore<AppState>(paths.StateFile);
        _logger = logger;
        _config = config;
        _cache = paths.CacheDirectory;
        var runner = new ProcessRunner(logger);
        _disks = new DiskService(runner, logger);
        _probe = new SystemProbe(runner, logger);
        _linux = new LinuxImageService(new HttpClient(), logger, config);
        _safety = new SafetyEngine();
        _usb = new UsbStorageService(runner, logger, _safety);
        _dependencies = new DependencyService(runner, logger);
        Persist();
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        Disks = await _disks.ScanAsync(ct);
        var selected = Disks.FirstOrDefault(d => d.Number == _state.SelectedDiskNumber && d.SafeCandidate && (string.IsNullOrWhiteSpace(_state.SelectedDiskUniqueId) || string.Equals(d.UniqueId, _state.SelectedDiskUniqueId, StringComparison.OrdinalIgnoreCase)));
        if (selected is null)
        {
            var fallback = Disks.FirstOrDefault(d => d.SafeCandidate);
            _state.SelectedDiskNumber = fallback?.Number ?? "";
            _state.SelectedDiskIdentity = fallback?.DisplayName ?? "";
            _state.SelectedDiskUniqueId = fallback?.UniqueId ?? "";
            _state.UpdatedAt = DateTimeOffset.UtcNow;
        }
        WslReady = await _probe.IsWslReadyAsync(ct);
        RcmConnected = await _probe.IsRcmConnectedAsync(ct);
        HekateDetected = await _probe.IsHekateDetectedAsync(ct);
        Persist();
    }

    public void SelectDisk(DiskInfo? disk)
    {
        if (disk is null || !_safety.IsSafeTarget(disk)) { _logger.Warn("Blocked attempt to select an unsafe disk."); return; }
        _state.SelectedDiskNumber = disk.Number;
        _state.SelectedDiskIdentity = disk.DisplayName;
        _state.SelectedDiskUniqueId = disk.UniqueId;
        Persist();
    }

    public bool IsSelectedDiskSafe() => _safety.IsSafeTarget(Disks.FirstOrDefault(d => d.Number == _state.SelectedDiskNumber));
    public string SelectedDiskSafetyText() => _safety.Explain(Disks.FirstOrDefault(d => d.Number == _state.SelectedDiskNumber));

    public void SaveAutoPlan(AutoPlan plan)
    {
        _state.AutoPlan = plan;
        Persist();
    }

    public async Task PreflightAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("MewNX requires Windows.");
        await _dependencies.EnsureAsync(installOptional: _config.Dependencies.AutoInstallMissing && _config.Dependencies.InstallWslIfMissing, ct);
        await RefreshAsync(ct);
        var target = Disks.FirstOrDefault(d => d.Number == _state.SelectedDiskNumber);
        _safety.DemandSafeTarget(target);
        SetStage(InstallationStage.EnvironmentPreflight, StageState.Completed, "Safety checks passed.");
        _state.CurrentStage = InstallationStage.LinuxImage;
        Persist();
        _logger.Info($"Preflight complete. Target: {target!.DisplayName}");
    }

    public async Task DownloadAndVerifyLinuxAsync(IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        _state.LinuxDownloaded = false;
        _state.LinuxVerified = false;
        SetStage(InstallationStage.LinuxImage, StageState.Running, "Downloading Linux image.");
        Persist();
        try
        {
            await _linux.DownloadAsync(_cache, progress, ct);
            var ok = await _linux.VerifySha1Async(_linux.FinalPath(_cache), ct);
            if (!ok) throw new InvalidDataException("Linux image SHA-1 verification failed.");
            _state.LinuxDownloaded = true;
            _state.LinuxVerified = true;
            SetStage(InstallationStage.LinuxImage, StageState.Completed, "Image verified successfully.");
            _state.CurrentStage = InstallationStage.UsbStoragePreparation;
            Persist();
        }
        catch (OperationCanceledException)
        {
            SetStage(InstallationStage.LinuxImage, StageState.WaitingForUser, "Download cancelled. Existing partial data was preserved for resumption.");
            Persist();
            throw;
        }
        catch (Exception ex)
        {
            SetStage(InstallationStage.LinuxImage, StageState.Failed, ex.Message);
            Persist();
            _logger.Error("Linux image download/verification failed", ex);
            throw;
        }
    }

    public async Task PrepareUsbAsync(IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var selected = Disks.FirstOrDefault(d => d.Number == _state.SelectedDiskNumber);
        _safety.DemandSafeTarget(selected);
        var current = await _disks.GetDiskAsync(_state.SelectedDiskNumber, ct);
        _safety.DemandStableIdentity(selected!, current!);
        if (!_state.LinuxVerified) throw new InvalidOperationException("The Linux image must be verified before writing the USB target.");
        if (!await _linux.VerifyExistingAsync(_cache, ct))
        {
            _state.LinuxDownloaded = false;
            _state.LinuxVerified = false;
            Persist();
            throw new InvalidDataException("The cached Linux image is missing, incomplete or failed verification. Download it again before writing the USB.");
        }
        SetStage(InstallationStage.UsbStoragePreparation, StageState.Running, "Preparing USB target and flashing Linux image.");
        _state.CurrentStage = InstallationStage.UsbStoragePreparation;
        Persist();
        var archive = _linux.FinalPath(_cache);
        var work = Path.Combine(_cache, "usb-work");
        await _usb.PrepareAndFlashAsync(current!, archive, work, progress, ct);
        SetStage(InstallationStage.UsbStoragePreparation, StageState.Completed, "Linux image flashed to USB successfully.");
        _state.CurrentStage = InstallationStage.HekateSd;
        Persist();
    }

    public void PauseForHardware()
    {
        SetStage(InstallationStage.UsbStoragePreparation, StageState.WaitingForUser, "Waiting for physical hardware procedure.");
        _state.CurrentStage = InstallationStage.UsbStoragePreparation;
        Persist();
    }

    private void SetStage(InstallationStage stage, StageState status, string message)
    {
        _state.EnsureStages();
        var record = _state.Stages.First(x => x.Stage == stage);
        record.State = status;
        record.Message = message;
        if (status == StageState.Completed) record.CompletedAt = DateTimeOffset.UtcNow;
        _state.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Persist()
    {
        _state.UpdatedAt = DateTimeOffset.UtcNow;
        _store.Save(_state);
        StateChanged?.Invoke();
    }

    public void FailCurrentStage(string message)
    {
        var stage = _state.CurrentStage;
        if (stage == InstallationStage.Completed) return;
        SetStage(stage, StageState.Failed, message);
        Persist();
    }

    public void WarnCurrentStage(string message)
    {
        var stage = _state.CurrentStage;
        if (stage == InstallationStage.Completed) return;
        SetStage(stage, StageState.Warning, message);
        Persist();
    }
}
