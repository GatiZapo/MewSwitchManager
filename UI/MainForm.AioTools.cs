using MewNX.Core;
using MewNX.Hardware;
using MewNX.Infrastructure;
using MewNX.Models;

namespace MewNX.UI;

public sealed partial class MainForm
{
    private SwitchToolInstaller _toolInstaller = null!;
    private readonly Button _aioToolsButton = new();
    private readonly Button _aioAllButton = new();
    private readonly Button _aioScanButton = new();
    private readonly ComboBox _aioPackSelector = new();
    private readonly ListView _aioToolList = new();
    private readonly Label _aioScanStatus = new();

    private void InitializeAioTools(AppPaths paths)
    {
        _toolInstaller = new SwitchToolInstaller(paths, _logger);
        _content.RowCount = Math.Max(_content.RowCount, 11);
        _content.Controls.Add(BuildAioToolsSection(), 0, 7);
        _content.Controls.Add(BuildEmulationSection(), 0, 8);
        _content.Controls.Add(BuildGameCenterSection(), 0, 9);
    }

    private Control BuildAioToolsSection()
    {
        var card = CreateCard();
        card.Height = 390;
        card.Padding = new Padding(16);
        var title = new Label { Dock = DockStyle.Top, Height = 28, Text = "AIO TOOLS — NINITE MODE", ForeColor = Theme.Text, Font = Theme.UI(12, FontStyle.Bold) };
        var info = new Label { Dock = DockStyle.Top, Height = 36, Text = "Marca herramientas para instalar/actualizar. Un SCAN marca automáticamente las que ya existen en la Switch.", ForeColor = Theme.Muted, Font = Theme.Mono(7.5f) };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false, BackColor = Theme.Surface, Padding = new Padding(0, 4, 0, 4) };
        _aioPackSelector.DropDownStyle = ComboBoxStyle.DropDownList; _aioPackSelector.Width = 245; _aioPackSelector.BackColor = Theme.Surface2; _aioPackSelector.ForeColor = Theme.Text; _aioPackSelector.Font = Theme.UI(8.5f);
        _aioPackSelector.Items.Add("PACKS — seleccionar...");
        foreach (var pack in SwitchToolCatalog.Packs) _aioPackSelector.Items.Add(pack);
        _aioPackSelector.SelectedIndex = 0; _aioPackSelector.SelectedIndexChanged += (_, _) => ApplySelectedPack();
        StyleButton(_aioScanButton, "SCAN SWITCH", Theme.Blue, 125);
        StyleButton(_aioToolsButton, "INSTALL / UPDATE SELECTED", Theme.Pink, 190);
        StyleButton(_aioAllButton, "UPDATE ALL SAFE", Theme.Blue, 145);
        _aioScanButton.Click += async (_, _) => await ScanAioAsync();
        _aioToolsButton.Click += async (_, _) => await AioInstallSelectedAsync();
        _aioAllButton.Click += async (_, _) => await AioInstallAllAsync();
        toolbar.Controls.Add(_aioPackSelector); toolbar.Controls.Add(_aioScanButton); toolbar.Controls.Add(_aioToolsButton); toolbar.Controls.Add(_aioAllButton);
        ConfigureAioToolList();
        var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0), BackColor = Theme.Surface };
        listHost.Controls.Add(_aioToolList);
        _aioScanStatus.Dock = DockStyle.Bottom; _aioScanStatus.Height = 22; _aioScanStatus.ForeColor = Theme.Muted; _aioScanStatus.Font = Theme.Mono(7); _aioScanStatus.Text = "Not scanned — connect/mount the Switch SD card first.";
        card.Controls.Add(listHost); card.Controls.Add(_aioScanStatus); card.Controls.Add(toolbar); card.Controls.Add(info); card.Controls.Add(title);
        return card;
    }

    private void ConfigureAioToolList()
    {
        _aioToolList.Dock = DockStyle.Fill; _aioToolList.View = View.Details; _aioToolList.CheckBoxes = true; _aioToolList.FullRowSelect = true; _aioToolList.HideSelection = false; _aioToolList.MultiSelect = false;
        _aioToolList.BackColor = Theme.Surface2; _aioToolList.ForeColor = Theme.Text; _aioToolList.BorderStyle = BorderStyle.None; _aioToolList.Font = Theme.UI(8.5f);
        _aioToolList.Columns.Add("", 30); _aioToolList.Columns.Add("TOOL", 180); _aioToolList.Columns.Add("TYPE", 90); _aioToolList.Columns.Add("STATUS", 190); _aioToolList.Columns.Add("DESCRIPTION", 500);
        foreach (var definition in SwitchToolCatalog.Definitions)
        {
            var item = new ListViewItem("") { Tag = definition.Id };
            item.SubItems.Add(definition.Name); item.SubItems.Add(definition.Kind.ToString().ToUpperInvariant()); item.SubItems.Add("Not scanned"); item.SubItems.Add(definition.Description);
            _aioToolList.Items.Add(item);
        }
    }

    private void ApplySelectedPack()
    {
        if (_aioPackSelector.SelectedItem is not SwitchToolPack pack) return;
        var ids = pack.ToolIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ListViewItem item in _aioToolList.Items)
            if (item.Tag is string id) item.Checked = ids.Contains(id);
        _aioScanStatus.Text = $"Pack: {pack.Name} — {ids.Count} tools selected. Scan again to refresh installed status.";
        _aioPackSelector.SelectedIndex = 0;
    }

    private bool TryGetAioTarget(out string root)
    {
        root = "";
        var target = _content.Controls.OfType<Control>().SelectMany(Flatten).OfType<ComboBox>().FirstOrDefault(c => c.Items.Cast<object>().Any(x => x is RemovableDrive));
        if (target?.SelectedItem is RemovableDrive drive) { root = drive.Root; return true; }
        var first = new RemovableDriveService().Scan().FirstOrDefault();
        if (first is null) return false;
        root = first.Root;
        return true;
    }

    private static IEnumerable<Control> Flatten(Control c)
    {
        yield return c;
        foreach (Control child in c.Controls)
            foreach (var x in Flatten(child)) yield return x;
    }

    private static SwitchToolDefinition? GetToolDefinition(ListViewItem item)
        => item.Tag is string id ? SwitchToolCatalog.Definitions.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) : null;

    private async Task ScanAioAsync()
    {
        if (!TryGetAioTarget(out var root)) { _aioScanStatus.Text = "No Switch SD/removable target detected."; return; }
        try
        {
            _aioScanButton.Enabled = false; SetStatus("●  SCANNING SWITCH TOOLS", Theme.Blue);
            var statuses = await new SwitchToolManager(_logger).ScanAsync(root);
            var installed = 0;
            foreach (ListViewItem item in _aioToolList.Items)
            {
                var definition = GetToolDefinition(item);
                if (definition is null) continue;
                var status = statuses.FirstOrDefault(s => s.Definition.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase));
                if (status is null) continue;
                item.SubItems[3].Text = status.Installed ? $"✓ INSTALLED • {status.InstalledVersion} • latest {status.AvailableVersion}" : $"NOT INSTALLED • latest {status.AvailableVersion}";
                item.Checked = status.Installed;
                if (status.Installed) installed++;
            }
            _aioScanStatus.Text = $"Scan complete — {installed}/{statuses.Count} detected. Checked items can be installed or updated."; SetStatus("●  SWITCH TOOL SCAN COMPLETE", Theme.Green);
        }
        catch (Exception ex) { _logger.Error("AIO tool scan failed", ex); _aioScanStatus.Text = "Scan failed: " + ex.Message; SetStatus("●  TOOL SCAN FAILED", Theme.Red); }
        finally { _aioScanButton.Enabled = true; }
    }

    private async Task AioInstallSelectedAsync()
    {
        if (!TryGetAioTarget(out var root)) { MessageBox.Show(this, "No se detectó la microSD/almacenamiento de la Switch.", "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var selected = _aioToolList.Items.Cast<ListViewItem>().Where(x => x.Checked).Select(GetToolDefinition).Where(x => x is not null).Cast<SwitchToolDefinition>().ToList();
        if (selected.Count == 0) { MessageBox.Show(this, "Selecciona al menos una herramienta.", "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (MessageBox.Show(this, $"Se instalarán/actualizarán {selected.Count} herramientas seleccionadas. Los archivos existentes se respaldan antes de sustituirse.\n\n¿Continuar?", "AIO Tools", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _operationCts = new CancellationTokenSource();
        try
        {
            _aioToolsButton.Enabled = false; SetStatus($"●  INSTALLING {selected.Count} TOOLS", Theme.Pink); var ok = 0;
            foreach (var definition in selected) { _operationCts.Token.ThrowIfCancellationRequested(); try { await _toolInstaller.InstallOrUpdateAsync(definition, root, new Progress<DownloadProgress>(RenderDownloadProgress), _operationCts.Token); ok++; } catch (Exception ex) { _logger.Warn($"Skipping {definition.Name}: {ex.Message}"); } }
            SetStatus($"●  TOOL INSTALL COMPLETE {ok}/{selected.Count}", ok == selected.Count ? Theme.Green : Theme.Amber); await ScanAioAsync();
            MessageBox.Show(this, $"Operación terminada.\n\nCorrectas: {ok}/{selected.Count}.", "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { SetStatus("●  TOOL INSTALL CANCELLED", Theme.Amber); }
        finally { _aioToolsButton.Enabled = true; _operationCts.Dispose(); _operationCts = null; }
    }

    private async Task AioInstallAllAsync()
    {
        if (!TryGetAioTarget(out var root)) { MessageBox.Show(this, "No se detectó la microSD/almacenamiento de la Switch.", "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (MessageBox.Show(this, "UPDATE ALL SAFE actualizará únicamente el catálogo de herramientas. No toca NAND, emuMMC, boot0/boot1 ni Linux.\n\n¿Continuar?", "UPDATE ALL SAFE", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _operationCts = new CancellationTokenSource();
        try { _aioAllButton.Enabled = false; SetStatus("●  UPDATING ALL SAFE TOOLS", Theme.Pink); var results = await _toolInstaller.InstallAllSafeAsync(root, SwitchToolCatalog.Definitions, new Progress<DownloadProgress>(RenderDownloadProgress), _operationCts.Token); SetStatus($"●  SAFE UPDATE {results.Count}/{SwitchToolCatalog.Definitions.Count}", Theme.Green); await ScanAioAsync(); MessageBox.Show(this, $"UPDATE ALL SAFE terminado.\n\nActualizados correctamente: {results.Count}/{SwitchToolCatalog.Definitions.Count}.", "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (OperationCanceledException) { SetStatus("●  SAFE UPDATE CANCELLED", Theme.Amber); }
        finally { _aioAllButton.Enabled = true; _operationCts.Dispose(); _operationCts = null; }
    }
}
