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
                ? "No safe USB target detected. Connect the intended USB drive and press REFRESH."
                : $"Protected by Safety Engine • {selected.Model} • {selected.SizeGb:0.0} GB • {selected.BusType}";
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
        var verified = _engine.State.LinuxVerified;
        _progress.Caption = verified ? "READY FOR USB WRITE" : _engine.State.CurrentStage == InstallationStage.LinuxImage ? "LINUX IMAGE" : "PREFLIGHT";
        _progress.Detail = verified
            ? "Image verified. The next action is destructive and requires two safety gates plus confirmation."
            : "No destructive operation is started until the target identity is validated.";
        _progress.Value = verified ? 100 : _engine.State.Stages.FirstOrDefault(s => s.Stage == _engine.State.CurrentStage)?.State == StageState.Completed ? 25 : 0;
        _progress.RightText = verified ? "100%" : "READY";
        _progress.Invalidate();
    }

    private void UpdateActionButtons()
    {
        var preflightDone = _engine.State.Stages.Any(s => s.Stage == InstallationStage.EnvironmentPreflight && s.State == StageState.Completed);
        var imageVerified = _engine.State.LinuxVerified;
        var busy = _operationCts is not null;
        _preflight.Enabled = !busy && !preflightDone;
        _download.Enabled = !busy && preflightDone && !imageVerified;
        _start.Enabled = !busy && imageVerified && _engine.IsSelectedDiskSafe();
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
            SetStatus("●  PREPARING USB", Theme.Pink);
            _progress.Caption = "PREPARING USB";
            _progress.Detail = "Starting safety checks...";
            _progress.Value = 0;
            _progress.RightText = "STARTING";
            _progress.Invalidate();

            var progress = new Progress<DownloadProgress>(p =>
            {
                switch (p.Phase)
                {
                    case "EXTRACTING LINUX IMAGE":
                        _progress.Caption = "EXTRACTING LINUX IMAGE";
                        _progress.Value = p.TotalBytes is > 0 ? p.BytesReceived * 100d / p.TotalBytes.Value : 0;
                        _progress.Detail = p.TotalBytes is > 0
                            ? $"Processing archive entries: {p.BytesReceived:N0} / {p.TotalBytes.Value:N0}"
                            : "Extracting archive...";
                        _progress.RightText = $"{_progress.Value:0}%";
                        SetStatus("●  EXTRACTING IMAGE", Theme.Blue);
                        break;
                    case "BUILDING LINUX IMAGE":
                        _progress.Caption = "BUILDING LINUX IMAGE";
                        _progress.Value = p.TotalBytes is > 0 ? p.BytesReceived * 100d / p.TotalBytes.Value : 0;
                        _progress.Detail = $"Merging image parts: {p.BytesReceived:N0} / {p.TotalBytes ?? 0:N0}";
                        _progress.RightText = $"{_progress.Value:0}%";
                        SetStatus("●  BUILDING IMAGE", Theme.Blue);
                        break;
                    case "VERIFYING TARGET":
                        _progress.Caption = "VERIFYING TARGET";
                        _progress.Value = 0;
                        _progress.Detail = "Re-checking the USB identity before any destructive operation.";
                        _progress.RightText = "CHECK";
                        SetStatus("●  VERIFYING TARGET", Theme.Blue);
                        break;
                    case "PARTITIONING USB":
                        _progress.Caption = "PARTITIONING USB";
                        _progress.Value = 0;
                        _progress.Detail = "Creating the target partition. The USB is being modified now.";
                        _progress.RightText = "WORKING";
                        SetStatus("●  PARTITIONING USB", Theme.Pink);
                        break;
                    case "FLASHING USB":
                        _progress.Caption = "FLASHING USB";
                        _progress.Value = p.TotalBytes is > 0 ? p.BytesReceived * 100d / p.TotalBytes.Value : 0;
                        _progress.Detail = $"Writing Linux image: {FormatBytes(p.BytesReceived)} / {FormatBytes(p.TotalBytes ?? 0)}";
                        _progress.RightText = $"{_progress.Value:0}%";
                        SetStatus("●  FLASHING USB", Theme.Pink);
                        break;
                    case "USB FLASH COMPLETE":
                        _progress.Caption = "USB READY";
                        _progress.Value = 100;
                        _progress.Detail = "Linux image written successfully. Continue with the Hekate/SD part of the setup.";
                        _progress.RightText = "100%";
                        SetStatus("●  USB READY", Theme.Green);
                        break;
                    default:
                        _progress.Caption = p.Phase;
                        _progress.Value = p.TotalBytes is > 0 ? p.BytesReceived * 100d / p.TotalBytes.Value : 0;
                        _progress.Detail = "Working...";
                        _progress.RightText = p.TotalBytes is > 0 ? $"{_progress.Value:0}%" : "WORKING";
                        break;
                }
                _progress.Invalidate();
            });

            await _engine.PrepareUsbAsync(progress, _operationCts.Token);
            _progress.Value = 100;
            _progress.Caption = "USB READY";
            _progress.Detail = "Linux image written successfully. Continue with the Hekate/SD part of the setup.";
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
