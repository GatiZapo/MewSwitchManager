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
    public InstallationStage ResumeStage => _state.GetResumeStage();
    public IReadOnlyList<DiskInfo> Disks { get; private set; } = [];
    public bool WslReady { get; private set; }
    public bool RcmConnected { get; private set; }
    public bool HekateDetected { get; private set; }

    public event Action? StateChanged;

    public InstallationEngine(AppPaths paths, AppConfig config, AppLogger logger)
    {
        _state = new JsonStore<AppState>(paths.StateFile).LoadOrCreate();
        _state.EnsureStages();
        _store = new JsonStore<AppState>(paths.StateFile);
        _logger = logger;
        _config = config;
        _cache = paths.CacheDirectory;
        var runner = new ProcessRunner();
        _disks = new DiskService(runner, logger);
        _probe = new SystemProbe(runner, logger);
        _linux = new LinuxImageService(new HttpClient(), logger, config);
        _safety = new SafetyEngine();
        _usb = new UsbStorageService(runner, logger, _safety);
        _dependencies = new DependencyService(runner, logger);

        if (!string.Equals(_state.LastKnownAppVersion, _config.AppVersion, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info($"State loaded from previous application version: {_state.LastKnownAppVersion ?? "unknown"} -> {_config.AppVersion}.");
            _state.LastKnownAppVersion = _config.AppVersion;
            Persist();
        }
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        Disks = await _disks.ScanAsync(ct);

        // Never silently replace a remembered USB with another drive. A missing or
        // changed device must remain unselected until the user explicitly chooses one.
        var selected = string.IsNullOrWhiteSpace(_state.SelectedDiskNumber)
            ? null
            : Disks.FirstOrDefault(d =>
                d.Number == _state.SelectedDiskNumber &&
                d.SafeCandidate &&
                !d.Protected &&
                string.Equals(d.BusType, "USB", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(_state.SelectedDiskUniqueId) ||
                 string.Equals(d.UniqueId, _state.SelectedDiskUniqueId, StringComparison.OrdinalIgnoreCase)));

        if (selected is null && !string.IsNullOrWhiteSpace(_state.SelectedDiskNumber))
        {
            _logger.Warn($"Remembered USB target {_state.SelectedDiskNumber} is not present with the expected identity. No replacement target was selected automatically.");
            _state.SelectedDiskNumber = "";
            _state.SelectedDiskIdentity = "";
            _state.SelectedDiskUniqueId = "";
        }

        WslReady = await _probe.IsWslReadyAsync(ct);
        RcmConnected = await _probe.IsRcmConnectedAsync(ct);
        HekateDetected = await _probe.IsHekateDetectedAsync(ct);

        ReconcilePersistedState();
        Persist();
    }

    private void ReconcilePersistedState()
    {
        _state.EnsureStages();

        if (_state.LinuxVerified)
        {
            var imagePath = _linux.FinalPath(_cache);
            if (!File.Exists(imagePath))
            {
                InvalidateLinuxVerification("Verified Linux image is no longer present in the cache.");
            }
            else
            {
                var info = new FileInfo(imagePath);
                var fingerprintKnown = _state.LinuxVerifiedSizeBytes > 0 && _state.LinuxVerifiedLastWriteUtc.HasValue;
                var unchanged = fingerprintKnown &&
                                info.Length == _state.LinuxVerifiedSizeBytes &&
                                info.LastWriteTimeUtc == _state.LinuxVerifiedLastWriteUtc.Value.UtcDateTime;

                if (!fingerprintKnown)
                {
                    // Legacy state from 0.2: keep the flag for now, but the next
                    // destructive workflow still performs a complete SHA-1 check.
                    _logger.Info("Legacy Linux verification state detected; destructive operations will re-verify the image.");
                }
                else if (!unchanged)
                {
                    InvalidateLinuxVerification("Cached Linux image changed since it was verified.");
                }
            }
        }

        // Hekate can be detected from the SD card mounted in Windows. Only advance
        // this physical checkpoint if the USB stage is already known to be complete.
        if (_state.IsStageComplete(InstallationStage.UsbStoragePreparation) &&
            !_state.IsStageComplete(InstallationStage.HekateSd) &&
            HekateDetected)
        {
            SetStage(InstallationStage.HekateSd, StageState.Completed, "Hekate configuration detected on a mounted SD card.");
            _state.CurrentStage = InstallationStage.SwitchConfiguration;
            _logger.Info("Hekate/SD checkpoint auto-completed from detected SD card contents.");
        }
    }

    private void InvalidateLinuxVerification(string reason)
    {
        _state.LinuxDownloaded = false;
        _state.LinuxVerified = false;
        _state.LinuxVerifiedSizeBytes = 0;
        _state.LinuxVerifiedLastWriteUtc = null;
        SetStage(InstallationStage.LinuxImage, StageState.Warning, reason + " Download/verification is required again.");
        _state.CurrentStage = InstallationStage.LinuxImage;
        _logger.Warn(reason);
    }

    public void SelectDisk(DiskInfo? disk)
    {
        if (disk is null || !_safety.IsSafeTarget(disk))
        {
            _logger.Warn("Blocked attempt to select an unsafe disk.");
            return;
        }
        _state.SelectedDiskNumber = disk.Number;
        _state.SelectedDiskIdentity = disk.DisplayName;
        _state.SelectedDiskUniqueId = disk.UniqueId;
        Persist();
    }

    public bool IsSelectedDiskSafe() => _safety.IsSafeTarget(Disks.FirstOrDefault(d => d.Number == _state.SelectedDiskNumber));
    public string SelectedDiskSafetyText() => _safety.Explain(Disks.FirstOrDefault(d => d.Number == _state.SelectedDiskNumber));

    public async Task PreflightAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("MewSwitch Manager requires Windows.");
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
        _state.LinuxVerifiedSizeBytes = 0;
        _state.LinuxVerifiedLastWriteUtc = null;
        SetStage(InstallationStage.LinuxImage, StageState.Running, "Downloading Linux image.");
        Persist();
        await _linux.DownloadAsync(_cache, progress, ct);
        var path = _linux.FinalPath(_cache);
        var ok = await _linux.VerifySha1Async(path, ct);
        if (!ok) throw new InvalidDataException("Linux image SHA-1 verification failed.");

        var info = new FileInfo(path);
        _state.LinuxDownloaded = true;
        _state.LinuxVerified = true;
        _state.LinuxVerifiedSizeBytes = info.Length;
        _state.LinuxVerifiedLastWriteUtc = info.LastWriteTimeUtc;
        SetStage(InstallationStage.LinuxImage, StageState.Completed, "Image verified successfully.");
        _state.CurrentStage = InstallationStage.UsbStoragePreparation;
        Persist();
    }

    public async Task PrepareUsbAsync(IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var selected = Disks.FirstOrDefault(d => d.Number == _state.SelectedDiskNumber);
        _safety.DemandSafeTarget(selected);
        var current = await _disks.GetDiskAsync(_state.SelectedDiskNumber, ct);
        _safety.DemandStableIdentity(selected!, current!);
        if (!_state.LinuxVerified)
            throw new InvalidOperationException("The Linux image must be verified before writing the USB target.");

        // Never trust persisted verification for a destructive operation. The full
        // SHA-1 check remains mandatory immediately before the USB write.
        if (!await _linux.VerifyExistingAsync(_cache, ct))
        {
            InvalidateLinuxVerification("Cached image failed final destructive-operation re-verification.");
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
        SetStage(_state.CurrentStage, StageState.WaitingForUser, "Waiting for the physical/configuration procedure.");
        Persist();
    }

    public void MarkStageCompleted(InstallationStage stage, string message)
    {
        if (stage == InstallationStage.Completed)
        {
            if (_state.GetResumeStage() != InstallationStage.Completed)
                throw new InvalidOperationException("Cannot mark the installation completed while earlier checkpoints remain unfinished.");
            _state.CurrentStage = InstallationStage.Completed;
            _state.LastSuccessfulRunAt = DateTimeOffset.UtcNow;
            Persist();
            return;
        }

        var resume = _state.GetResumeStage();
        if (resume != stage)
            throw new InvalidOperationException($"Cannot complete {stage}; the next required checkpoint is {resume}.");

        SetStage(stage, StageState.Completed, message);
        var next = Enum.GetValues<InstallationStage>()
            .Where(x => x > stage && x != InstallationStage.Completed)
            .FirstOrDefault();
        _state.CurrentStage = next == default ? InstallationStage.Completed : next;
        if (_state.CurrentStage == InstallationStage.Completed)
            _state.LastSuccessfulRunAt = DateTimeOffset.UtcNow;
        Persist();
    }

    private void SetStage(InstallationStage stage, StageState status, string message)
    {
        _state.EnsureStages();
        var record = _state.Stages.First(x => x.Stage == stage);
        record.State = status;
        record.Message = message;
        if (status == StageState.Completed) record.CompletedAt = DateTimeOffset.UtcNow;
        else if (status is StageState.Running or StageState.Warning or StageState.Failed) record.CompletedAt = null;
        _state.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Persist()
    {
        _state.EnsureStages();
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
