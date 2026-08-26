using MewSwitchManager.Core;
using MewSwitchManager.Hardware;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private SwitchToolInstaller _toolInstaller = null!;
    private readonly Button _aioToolsButton = new();
    private readonly Button _aioAllButton = new();

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_toolInstaller is null) InitializeAioTools(AppPaths.Create(_config));
    }

    private void InitializeAioTools(AppPaths paths)
    {
        _toolInstaller = new SwitchToolInstaller(paths, _logger);
        _content.RowCount = Math.Max(_content.RowCount, 9);
        _content.Controls.Add(BuildAioToolsSection(), 0, 7);
    }

    private Control BuildAioToolsSection()
    {
        var card = CreateCard();
        card.Height = 150;
        card.Padding = new Padding(16);
        var title = new Label { Dock = DockStyle.Top, Height = 28, Text = "AIO TOOLS", ForeColor = Theme.Text, Font = Theme.UI(12, FontStyle.Bold) };
        var info = new Label { Dock = DockStyle.Top, Height = 38, Text = "Payloads • Homebrew • Overlays\nTegraExplorer / Lockpick_RCM / Sphaira / JKSV / Goldleaf / Tesla / sys-clk / Status Monitor", ForeColor = Theme.Muted, Font = Theme.Mono(7.2f) };
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Surface };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        StyleButton(_aioToolsButton, "INSTALL / UPDATE TOOL", Theme.Blue, 180);
        StyleButton(_aioAllButton, "UPDATE ALL SAFE", Theme.Pink, 180);
        _aioToolsButton.Click += async (_, _) => await AioInstallOneAsync();
        _aioAllButton.Click += async (_, _) => await AioInstallAllAsync();
        row.Controls.Add(_aioToolsButton, 0, 0); row.Controls.Add(_aioAllButton, 1, 0);
        card.Controls.Add(row); card.Controls.Add(info); card.Controls.Add(title);
        return card;
    }

    private bool TryGetAioTarget(out string root)
    {
        root = "";
        var target = _content.Controls.OfType<Control>().SelectMany(Flatten).OfType<ComboBox>().FirstOrDefault(c => c.Items.Cast<object>().Any(x => x is RemovableDrive));
        if (target?.SelectedItem is RemovableDrive drive) { root = drive.Root; return true; }
        var first = new RemovableDriveService().Scan().FirstOrDefault();
        if (first is null) return false;
        root = first.Root; return true;
    }

    private static IEnumerable<Control> Flatten(Control c)
    {
        yield return c;
        foreach (Control child in c.Controls) foreach (var x in Flatten(child)) yield return x;
    }

    private async Task AioInstallOneAsync()
    {
        if (!TryGetAioTarget(out var root)) { MessageBox.Show(this, "No se detectó la microSD/almacenamiento de la Switch.", "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        using var picker = new ToolPickerDialog(SwitchToolCatalog.Definitions);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.Selected is null) return;
        _operationCts = new CancellationTokenSource();
        try
        {
            _aioToolsButton.Enabled = false;
            SetStatus("●  AIO TOOL UPDATE", Theme.Pink);
            var result = await _toolInstaller.InstallOrUpdateAsync(picker.Selected, root, new Progress<DownloadProgress>(RenderDownloadProgress), _operationCts.Token);
            SetStatus("●  TOOL UPDATED", Theme.Green);
            MessageBox.Show(this, result.Message + "\n\nBackup: " + (string.IsNullOrWhiteSpace(result.BackupPath) ? "none needed" : result.BackupPath), "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { _logger.Error("AIO tool update failed", ex); SetStatus("●  TOOL UPDATE FAILED", Theme.Red); MessageBox.Show(this, ex.Message, "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { _aioToolsButton.Enabled = true; _operationCts.Dispose(); _operationCts = null; }
    }

    private async Task AioInstallAllAsync()
    {
        if (!TryGetAioTarget(out var root)) { MessageBox.Show(this, "No se detectó la microSD/almacenamiento de la Switch.", "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var confirm = MessageBox.Show(this, "UPDATE ALL SAFE actualizará únicamente el catálogo de herramientas. No toca NAND, emuMMC, boot0/boot1 ni Linux.\n\n¿Continuar?", "UPDATE ALL SAFE", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;
        _operationCts = new CancellationTokenSource();
        try
        {
            _aioAllButton.Enabled = false;
            SetStatus("●  UPDATING ALL SAFE TOOLS", Theme.Pink);
            var results = await _toolInstaller.InstallAllSafeAsync(root, SwitchToolCatalog.Definitions, new Progress<DownloadProgress>(RenderDownloadProgress), _operationCts.Token);
            SetStatus($"●  SAFE UPDATE {results.Count}/{SwitchToolCatalog.Definitions.Count}", Theme.Green);
            MessageBox.Show(this, $"UPDATE ALL SAFE terminado.\n\nActualizados correctamente: {results.Count}/{SwitchToolCatalog.Definitions.Count}.\n\nLos fallos se registran en el log y no detienen los demás componentes.", "AIO Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { SetStatus("●  SAFE UPDATE CANCELLED", Theme.Amber); }
        finally { _aioAllButton.Enabled = true; _operationCts.Dispose(); _operationCts = null; }
    }
}

internal sealed class ToolPickerDialog : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    public SwitchToolDefinition? Selected => _list.SelectedItem as SwitchToolDefinition;
    public ToolPickerDialog(IEnumerable<SwitchToolDefinition> definitions)
    {
        Text = "MewSwitch — Select Tool"; Width = 520; Height = 420; StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Surface; ForeColor = Theme.Text;
        foreach (var definition in definitions) _list.Items.Add(definition);
        _list.BackColor = Theme.Surface2; _list.ForeColor = Theme.Text; _list.Font = Theme.UI(9); _list.SelectedIndex = 0;
        var ok = new Button { Text = "INSTALL / UPDATE", Dock = DockStyle.Bottom, Height = 42, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "CANCEL", Dock = DockStyle.Bottom, Height = 34, DialogResult = DialogResult.Cancel };
        Controls.Add(_list); Controls.Add(cancel); Controls.Add(ok); AcceptButton = ok; CancelButton = cancel;
    }
}
