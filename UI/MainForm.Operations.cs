using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private async Task RefreshAsync()
    {
        if (_operationCts is not null) return;
        try
        {
            SetStatus("●  SCANNING", Theme.Blue);
            _refresh.Enabled = false;
            await _engine.RefreshAsync();
            UpdateUi();
            SetStatus("●  SYSTEM READY", Theme.Green);
        }
        catch (Exception ex)
        {
            _logger.Error("Refresh failed", ex);
            SetStatus("●  ERROR", Theme.Red);
            MessageBox.Show(this, "No se pudo actualizar el estado del sistema.\n\n" + ex.Message, "MewSwitch Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _refresh.Enabled = true;
            UpdateActionButtons();
        }
    }

    private void UpdateUi()
    {
        UpdateDiskSelector();
        UpdateStatusCards();
        UpdateStages();
        UpdateHealthPanel();
        UpdateResumePanel();
        UpdateActionButtons();
    }

    private void UpdateDiskSelector()
    {
        var selectedNumber = _engine.State.SelectedDiskNumber;
        var safeDisks = _engine.Disks.Where(d => d.SafeCandidate && !d.Protected && d.BusType.Equals("USB", StringComparison.OrdinalIgnoreCase)).ToList();
        _suppressDiskSelection = true;
        _diskSelector.BeginUpdate();
        try
        {
            _diskSelector.Items.Clear();
            foreach (var disk in safeDisks) _diskSelector.Items.Add(disk);
            var selected = safeDisks.FirstOrDefault(d => d.Number == selectedNumber);
            _diskSelector.SelectedItem = selected;
            _targetHint.Text = selected is null
                ? "No remembered USB target is currently present. Connect the intended drive and select it explicitly. MewSwitch will never substitute another drive automatically."
                : $"IDENTITY LOCKED • {selected.Model} • {selected.SizeGb:0.0} GB • {selected.BusType} • ID {selected.UniqueId}";
        }
        finally
        {
            _diskSelector.EndUpdate();
            _suppressDiskSelection = false;
        }
    }

    private void UpdateStatusCards()
    {
        _rcm.ValueText = _engine.RcmConnected ? "RCM connected" : "RCM not connected";
        _rcm.Accent = _engine.RcmConnected ? Theme.Green : Theme.Amber;
        _wsl.ValueText = _engine.WslReady ? "WSL ready" : "WSL unavailable";
        _wsl.Accent = _engine.WslReady ? Theme.Green : Theme.Amber;
        _linux.ValueText = _engine.State.LinuxVerified ? "Verified / SHA-1 OK" : "Image not verified";
        _linux.Accent = _engine.State.LinuxVerified ? Theme.Green : Theme.Amber;
        UpdateUsbCard();
    }

    private void UpdateStages()
    {
        _stages.Stages = _engine.State.Stages;
        _stages.Invalidate();
    }

    private void UpdateHealthPanel()
    {
        var stage = _engine.ResumeStage;
        var verified = _engine.State.LinuxVerified;
        _progress.Caption = stage switch
        {
            InstallationStage.EnvironmentPreflight => "PREFLIGHT REQUIRED",
            InstallationStage.LinuxImage => "LINUX IMAGE",
            InstallationStage.UsbStoragePreparation => verified ? "READY FOR USB WRITE" : "USB WORKFLOW BLOCKED",
            InstallationStage.HekateSd => "HEKATE / SD CHECKPOINT",
            InstallationStage.SwitchConfiguration => "SWITCH CONFIGURATION CHECKPOINT",
            InstallationStage.MewrootHandoff => "MEWROOT HANDOFF CHECKPOINT",
            InstallationStage.Completed => "INSTALLATION COMPLETE",
            _ => "RESUME ENGINE"
        };
        _progress.Detail = stage switch
        {
            InstallationStage.EnvironmentPreflight => "Run the environment and target safety checks. Completed checkpoints are not repeated.",
            InstallationStage.LinuxImage => "Download or resume the Linux image, then verify its SHA-1 before continuing.",
            InstallationStage.UsbStoragePreparation => "The image is verified. The final USB identity checks and destructive confirmation remain mandatory.",
            InstallationStage.HekateSd => "Physical checkpoint: prepare the Hekate/SD side. The manager will remember this checkpoint and can detect Hekate files when the SD is mounted.",
            InstallationStage.SwitchConfiguration => "Physical/configuration checkpoint: complete the Switch-side configuration, then mark this checkpoint complete.",
            InstallationStage.MewrootHandoff => "Final physical handoff checkpoint: complete the Mewroot/Linux handoff, then mark the installation complete.",
            InstallationStage.Completed => "All recorded checkpoints are complete. Nothing needs to be repeated.",
            _ => "Persisted workflow state loaded."
        };
        _progress.Value = stage == InstallationStage.Completed ? 100 :
            _engine.State.Stages.Count(s => s.State == StageState.Completed) * 100d / Math.Max(1, _engine.State.Stages.Count);
        _progress.RightText = stage == InstallationStage.Completed ? "DONE" : $"{_progress.Value:0}%";
        _progress.Invalidate();
    }

    private void UpdateResumePanel()
    {
        var stage = _engine.ResumeStage;
        if (stage == InstallationStage.Completed)
        {
            _resumeTitle.Text = "RESUME ENGINE // ALL CHECKPOINTS COMPLETE";
            _resumeTitle.ForeColor = Theme.Green;
            _resumeDetail.Text = _engine.State.LastSuccessfulRunAt.HasValue
                ? $"Installation state is complete. Last successful run: {_engine.State.LastSuccessfulRunAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}. No completed steps will be requested again."
                : "Installation state is complete. No completed steps will be requested again.";
            _resumeAction.Enabled = false;
            _resumeAction.Text = "CHECKPOINTS COMPLETE";
            return;
        }

        var completed = _engine.State.Stages.Count(s => s.State == StageState.Completed);
        var total = _engine.State.Stages.Count;
        _resumeTitle.Text = $"RESUME ENGINE // NEXT: {StageName(stage)}";
        _resumeTitle.ForeColor = stage is InstallationStage.HekateSd or InstallationStage.SwitchConfiguration or InstallationStage.MewrootHandoff ? Theme.Amber : Theme.Pink;
        _resumeDetail.Text = $"{completed}/{total} checkpoints complete. MewSwitch will skip completed work, preserve the current state across versions, and only ask for the next required action.\nCurrent state: {_engine.State.Stages.FirstOrDefault(s => s.Stage == stage)?.State.ToString().ToUpperInvariant() ?? "PENDING"}.";
        var physical = stage is InstallationStage.HekateSd or InstallationStage.SwitchConfiguration or InstallationStage.MewrootHandoff;
        _resumeAction.Visible = physical;
        _resumeAction.Enabled = physical && _operationCts is null;
        _resumeAction.Text = stage == InstallationStage.HekateSd && _engine.HekateDetected ? "DETECTED / REFRESH" : "MARK CHECKPOINT";
    }

    private static string StageName(InstallationStage stage) => stage switch
    {
        InstallationStage.EnvironmentPreflight => "ENVIRONMENT PREFLIGHT",
        InstallationStage.LinuxImage => "LINUX IMAGE",
        InstallationStage.UsbStoragePreparation => "USB / STORAGE PREPARATION",
        InstallationStage.HekateSd => "HEKATE / SD",
        InstallationStage.SwitchConfiguration => "SWITCH CONFIGURATION",
        InstallationStage.MewrootHandoff => "MEWROOT HANDOFF",
        InstallationStage.Completed => "COMPLETED",
        _ => stage.ToString().ToUpperInvariant()
    };

    private void MarkResumeCheckpoint()
    {
        var stage = _engine.ResumeStage;
        if (stage is not (InstallationStage.HekateSd or InstallationStage.SwitchConfiguration or InstallationStage.MewrootHandoff))
            return;

        var detail = stage switch
        {
            InstallationStage.HekateSd => "Hekate/SD checkpoint confirmed by the user.",
            InstallationStage.SwitchConfiguration => "Switch configuration checkpoint confirmed by the user.",
            InstallationStage.MewrootHandoff => "Mewroot/Linux handoff checkpoint confirmed by the user.",
            _ => "Checkpoint confirmed by the user."
        };

        try
        {
            _engine.MarkStageCompleted(stage, detail);
            SetStatus("●  CHECKPOINT SAVED", Theme.Green);
            UpdateUi();
        }
        catch (Exception ex)
        {
            _logger.Error("Could not save checkpoint", ex);
            MessageBox.Show(this, ex.Message, "Checkpoint blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateActionButtons()
    {
        var stage = _engine.ResumeStage;
        var imageVerified = _engine.State.LinuxVerified;
        var busy = _operationCts is not null;
        _preflight.Enabled = !busy && stage == InstallationStage.EnvironmentPreflight;
        _download.Enabled = !busy && stage == InstallationStage.LinuxImage;
        _start.Enabled = !busy && stage == InstallationStage.UsbStoragePreparation && imageVerified && _engine.IsSelectedDiskSafe();
        _cancel.Enabled = busy;
    }

    private void UpdateUsbCard()
    {
        var disk = _engine.Disks.FirstOrDefault(d => d.Number == _engine.State.SelectedDiskNumber);
        _usb.ValueText = disk is null ? "No target selected" : _engine.IsSelectedDiskSafe() ? $"Disk {disk.Number} safe" : "Target blocked";
        _usb.Accent = disk is not null && _engine.IsSelectedDiskSafe() ? Theme.Green : Theme.Red;
        _usb.Invalidate();
    }

    private async Task RunPreflightAsync()
    {
        try
        {
            SetStatus("●  PREFLIGHT", Theme.Blue);
            _preflight.Enabled = false;
            await _engine.PreflightAsync();
            SetStatus("●  PREFLIGHT PASSED", Theme.Green);
            UpdateUi();
        }
        catch (Exception ex)
        {
            _engine.FailCurrentStage("Preflight failed. Review dependencies and target safety.");
            _logger.Error("Preflight failed", ex);
            SetStatus("●  PREFLIGHT BLOCKED", Theme.Red);
            MessageBox.Show(this, ex.Message, "Preflight blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateUi();
        }
        finally { UpdateActionButtons(); }
    }

    private async Task RunDownloadAsync()
    {
        if (!Uri.TryCreate(_config.LinuxImage.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "La URL de la imagen Linux no es válida. Revisa appsettings.json.", "MewSwitch Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _operationCts = new CancellationTokenSource();
        try
        {
            SetStatus("●  DOWNLOADING", Theme.Pink);
            _progress.Caption = "DOWNLOADING LINUX";
            _progress.Value = 0;
            var progress = new Progress<DownloadProgress>(p =>
            {
                _progress.Value = p.TotalBytes is > 0 ? p.BytesReceived * 100d / p.TotalBytes.Value : 0;
                _progress.Detail = $"{FormatBytes(p.BytesReceived)} / {FormatBytes(p.TotalBytes ?? 0)}";
                _progress.RightText = $"{FormatSpeed(p.SpeedBytesPerSecond)}  {FormatEta(p.Eta)}";
                _progress.Invalidate();
            });
            await _engine.DownloadAndVerifyLinuxAsync(progress, _operationCts.Token);
            _progress.Value = 100;
            _progress.Caption = "IMAGE VERIFIED";
            _progress.Detail = "The archive is complete and its SHA-1 matches the expected release hash.";
            _progress.RightText = "100%";
            SetStatus("●  IMAGE VERIFIED", Theme.Green);
            UpdateUi();
        }
        catch (OperationCanceledException)
        {
            _engine.WarnCurrentStage("Download paused. The partial file was preserved for resume.");
            _logger.Warn("Download cancelled. The partial file has been preserved for resume.");
            SetStatus("●  PAUSED", Theme.Amber);
        }
        catch (Exception ex)
        {
            _engine.FailCurrentStage("Linux image download or verification failed.");
            _logger.Error("Download/verification failed", ex);
            SetStatus("●  ERROR", Theme.Red);
            MessageBox.Show(this, ex.Message, "Linux image error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateUi();
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            UpdateActionButtons();
        }
    }

    private async Task StartInstallationAsync()
    {
        var disk = _engine.Disks.FirstOrDefault(d => d.Number == _engine.State.SelectedDiskNumber);
        if (disk is null || !_engine.IsSelectedDiskSafe())
        {
            MessageBox.Show(this, "Selecciona un USB que aparezca como objetivo seguro.", "Target blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!_config.Safety.AllowDestructiveOperations)
        {
            MessageBox.Show(this, "Las operaciones destructivas están deshabilitadas en la configuración.", "Safety Engine", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirmation = MessageBox.Show(this,
            $"SE VA A BORRAR TODO EL CONTENIDO DEL USB.\n\nDisk {disk.Number}\n{disk.Model}\n{disk.SizeGb:0.0} GB\n\nMewSwitch volverá a comprobar la identidad del dispositivo antes de limpiar y antes de escribir.\n\n¿Continuar?",
            "MewSwitch Manager — DESTRUCTIVE ACTION",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes) return;
        if (!ConfirmDestructiveWrite(disk)) return;

        _operationCts = new CancellationTokenSource();
        try
        {
            SetStatus("●  FLASHING USB", Theme.Pink);
            _progress.Caption = "FLASHING USB";
            _progress.Value = 0;
            var progress = new Progress<DownloadProgress>(p =>
            {
                _progress.Value = p.TotalBytes is > 0 ? p.BytesReceived * 100d / p.TotalBytes.Value : 0;
                _progress.Detail = $"{FormatBytes(p.BytesReceived)} / {FormatBytes(p.TotalBytes ?? 0)}";
                _progress.RightText = p.TotalBytes is > 0 ? $"{_progress.Value:0}%" : "WRITING";
                _progress.Invalidate();
            });
            await _engine.PrepareUsbAsync(progress, _operationCts.Token);
            _progress.Value = 100;
            _progress.Caption = "USB READY";
            _progress.Detail = "Linux image written successfully. Continue with the Hekate/SD checkpoint; completed checkpoints will be remembered.";
            _progress.RightText = "100%";
            SetStatus("●  USB READY", Theme.Green);
            UpdateUi();
        }
        catch (OperationCanceledException)
        {
            _engine.WarnCurrentStage("USB write cancelled. The target may contain a partial image and must be verified before reuse.");
            _logger.Warn("USB write cancelled. The target may contain a partially written image; verify it before reuse.");
            SetStatus("●  CANCELLED", Theme.Amber);
            MessageBox.Show(this, "La escritura se canceló. No vuelvas a arrancar desde el USB sin comprobar su estado.", "Write cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _engine.FailCurrentStage("USB preparation or Linux image flashing failed.");
            _logger.Error("USB preparation failed", ex);
            SetStatus("●  ERROR", Theme.Red);
            MessageBox.Show(this, "No se pudo preparar el USB.\n\n" + ex.Message, "USB write error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateUi();
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            UpdateActionButtons();
        }
    }

}
