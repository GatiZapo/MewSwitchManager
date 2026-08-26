using MewSwitchManager.Core;
using MewSwitchManager.Hardware;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private SwitchComponentManager _componentManager = null!;
    private readonly ComboBox _switchStorageSelector = new();
    private readonly ComboBox _componentSelector = new();
    private readonly Button _componentScan = new();
    private readonly Button _componentInstall = new();
    private readonly Button _rcmGuide = new();
    private readonly Label _componentStatus = new();
    private readonly Label _rcmManagerStatus = new();

    private void InitializeManagerCenter(AppPaths paths)
    {
        _componentManager = new SwitchComponentManager(paths, _logger);
        _content.RowCount = Math.Max(_content.RowCount, 8);
        _content.Controls.Add(BuildManagerSection(), 0, 6);

        var nav = _sidebar.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if (nav is not null)
        {
            nav.Height = 320;
            var button = NavigationButton("05   SWITCH MANAGER", false, 6);
            button.Width = 194;
            nav.Controls.Add(button);
        }
    }

    private Control BuildManagerSection()
    {
        var card = CreateCard();
        card.Height = 340;
        card.Padding = new Padding(16);

        var title = new Label { Dock = DockStyle.Top, Height = 30, Text = "SWITCH MANAGER", ForeColor = Theme.Text, Font = Theme.UI(13, FontStyle.Bold) };
        var subtitle = new Label { Dock = DockStyle.Top, Height = 28, Text = "Detect, download and update Hekate, Atmosphère and DBI directly on the mounted Switch storage.", ForeColor = Theme.Muted, Font = Theme.UI(8.3f) };

        var targetRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 3, BackColor = Theme.Surface, Margin = Padding.Empty };
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        _switchStorageSelector.Dock = DockStyle.Fill;
        _switchStorageSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _switchStorageSelector.BackColor = Theme.Surface2;
        _switchStorageSelector.ForeColor = Theme.Text;
        StyleButton(_componentScan, "SCAN", Theme.Blue, 104);
        StyleButton(_componentInstall, "INSTALL / UPDATE", Theme.Pink, 140);
        _componentScan.Click += async (_, _) => await ScanSwitchComponentsAsync();
        _componentInstall.Click += async (_, _) => await InstallSelectedComponentAsync();
        targetRow.Controls.Add(_switchStorageSelector, 0, 0);
        targetRow.Controls.Add(_componentScan, 1, 0);
        targetRow.Controls.Add(_componentInstall, 2, 0);

        var componentRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 40, ColumnCount = 2, BackColor = Theme.Surface, Margin = Padding.Empty };
        componentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        componentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _componentSelector.Dock = DockStyle.Fill;
        _componentSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _componentSelector.BackColor = Theme.Surface2;
        _componentSelector.ForeColor = Theme.Text;
        foreach (var component in _componentManager.Components) _componentSelector.Items.Add(component);
        _componentSelector.SelectedIndex = 0;
        _componentStatus.Text = "Connect the Switch SD card and press SCAN.";
        _componentStatus.Dock = DockStyle.Fill;
        _componentStatus.ForeColor = Theme.Muted;
        _componentStatus.Font = Theme.Mono(7.8f);
        componentRow.Controls.Add(_componentSelector, 0, 0);
        componentRow.Controls.Add(_componentStatus, 1, 0);

        var rcmRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 50, ColumnCount = 2, BackColor = Theme.Surface, Margin = Padding.Empty };
        rcmRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rcmRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        _rcmManagerStatus.Text = "RCM is detected independently from storage management.";
        _rcmManagerStatus.Dock = DockStyle.Fill;
        _rcmManagerStatus.ForeColor = Theme.Muted;
        _rcmManagerStatus.Font = Theme.Mono(7.5f);
        StyleButton(_rcmGuide, "RCM GUIDE", Theme.Blue, 120);
        _rcmGuide.Click += (_, _) => ShowRcmGuide();
        rcmRow.Controls.Add(_rcmManagerStatus, 0, 0);
        rcmRow.Controls.Add(_rcmGuide, 1, 0);

        var warning = new Label
        {
            Dock = DockStyle.Fill,
            Text = "SAFE UPDATE MODEL\n• Downloads are cached and resumable.\n• Archives are extracted into staging with path-traversal protection.\n• Existing bootloader / Atmosphère / DBI data is backed up before replacement.\n• Updates merge files; user configuration is not deleted.\n• Linux remains a separate destructive workflow.",
            ForeColor = Theme.Muted,
            Font = Theme.Mono(7.6f),
            Padding = new Padding(0, 10, 0, 0)
        };

        card.Controls.Add(warning);
        card.Controls.Add(rcmRow);
        card.Controls.Add(componentRow);
        card.Controls.Add(targetRow);
        card.Controls.Add(subtitle);
        card.Controls.Add(title);
        return card;
    }

    private void RefreshSwitchStorageTargets()
    {
        var targets = _componentManager.ScanTargets();
        _switchStorageSelector.BeginUpdate();
        try
        {
            _switchStorageSelector.Items.Clear();
            foreach (var target in targets) _switchStorageSelector.Items.Add(target);
            if (_switchStorageSelector.Items.Count > 0) _switchStorageSelector.SelectedIndex = 0;
        }
        finally { _switchStorageSelector.EndUpdate(); }
    }

    private async Task ScanSwitchComponentsAsync()
    {
        if (_operationCts is not null) return;
        RefreshSwitchStorageTargets();
        if (_switchStorageSelector.SelectedItem is not RemovableDrive target)
        {
            _componentStatus.Text = "No removable storage target detected.";
            return;
        }
        try
        {
            _componentScan.Enabled = false;
            _componentStatus.Text = "Querying official release channels...";
            var statuses = await _componentManager.ScanAsync(target.Root);
            _componentStatus.Text = string.Join("   ", statuses.Where(x => x.Definition.Id is SwitchComponent.Hekate or SwitchComponent.Atmosphere or SwitchComponent.Dbi)
                .Select(x => $"{x.Definition.Name}: {x.InstalledVersion} → {x.AvailableVersion}"));
            SetStatus("●  COMPONENTS SCANNED", Theme.Green);
        }
        catch (Exception ex)
        {
            _logger.Error("Component scan failed", ex);
            _componentStatus.Text = ex.Message;
            SetStatus("●  COMPONENT SCAN FAILED", Theme.Red);
        }
        finally { _componentScan.Enabled = true; }
    }

    private async Task InstallSelectedComponentAsync()
    {
        if (_switchStorageSelector.SelectedItem is not RemovableDrive target || _componentSelector.SelectedItem is not ComponentDefinition component)
        {
            MessageBox.Show(this, "Selecciona la microSD/almacenamiento de la Switch y un componente.", "Switch Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (component.Id is SwitchComponent.Linux or SwitchComponent.Tools)
        {
            MessageBox.Show(this, "Este componente todavía se gestiona desde su workflow específico.", "Switch Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(this,
            $"Se actualizará {component.Name} en {target.Root}.\n\nSe hará una copia de seguridad de los archivos relevantes antes de reemplazarlos y no se borrará la configuración existente.\n\n¿Continuar?",
            "Switch Manager — COMPONENT UPDATE",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        _operationCts = new CancellationTokenSource();
        try
        {
            _componentInstall.Enabled = false;
            SetStatus("●  UPDATING COMPONENT", Theme.Pink);
            _componentStatus.Text = $"Downloading {component.Name}...";
            var progress = new Progress<DownloadProgress>(RenderDownloadProgress);
            var result = await _componentManager.InstallOrUpdateAsync(component.Id, target.Root, progress, _operationCts.Token);
            _componentStatus.Text = $"✓ {result.Definition.Name}: {result.AvailableVersion} installed. Backup created before update.";
            SetStatus("●  COMPONENT UPDATED", Theme.Green);
        }
        catch (OperationCanceledException)
        {
            _componentStatus.Text = "Update cancelled. Existing installation was left untouched unless the merge had already started.";
            SetStatus("●  UPDATE CANCELLED", Theme.Amber);
        }
        catch (Exception ex)
        {
            _logger.Error("Component update failed", ex);
            _componentStatus.Text = ex.Message;
            SetStatus("●  COMPONENT UPDATE FAILED", Theme.Red);
            MessageBox.Show(this, ex.Message, "Component update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _componentInstall.Enabled = true;
            _operationCts.Dispose();
            _operationCts = null;
        }
    }

    private void ShowRcmGuide()
    {
        var probe = new RcmService(new ProcessRunner(), _logger);
        MessageBox.Show(this, probe.GetEntryGuide(), "RCM helper", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
