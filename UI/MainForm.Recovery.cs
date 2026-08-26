using MewSwitchManager.Core;
using MewSwitchManager.Infrastructure;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private readonly Button _recoveryCreate = new();
    private readonly Button _recoveryRestore = new();
    private readonly Button _recoveryOpen = new();
    private readonly Label _recoveryStatus = new();

    private void InitializeRecoveryCenter()
    {
        if (_content.Controls.GetControlFromPosition(0, 6) is not null) return;
        _content.RowCount = Math.Max(_content.RowCount, 10);
        _content.Controls.Add(BuildRecoverySection(), 0, 6);
    }

    private Control BuildRecoverySection()
    {
        var card = CreateCard();
        card.Height = 150;
        card.Padding = new Padding(16);
        var title = new Label { Dock = DockStyle.Top, Height = 28, Text = "RECOVERY CENTER", ForeColor = Theme.Text, Font = Theme.UI(12, FontStyle.Bold) };
        _recoveryStatus.Dock = DockStyle.Top;
        _recoveryStatus.Height = 28;
        _recoveryStatus.ForeColor = Theme.Muted;
        _recoveryStatus.Font = Theme.Mono(7.5f);
        _recoveryStatus.Text = "No checkpoint selected.";

        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, BackColor = Theme.Surface, Padding = new Padding(0, 4, 0, 0) };
        StyleButton(_recoveryCreate, "CREATE CHECKPOINT", Theme.Blue, 160);
        StyleButton(_recoveryRestore, "RESTORE CHECKPOINT", Theme.Amber, 170);
        StyleButton(_recoveryOpen, "OPEN DATA FOLDER", Theme.Surface2, 150);
        _recoveryCreate.Click += (_, _) => CreateRecoveryCheckpoint();
        _recoveryRestore.Click += (_, _) => RestoreRecoveryCheckpoint();
        _recoveryOpen.Click += (_, _) => OpenRecoveryDataFolder();
        row.Controls.Add(_recoveryCreate); row.Controls.Add(_recoveryRestore); row.Controls.Add(_recoveryOpen);
        card.Controls.Add(row); card.Controls.Add(_recoveryStatus); card.Controls.Add(title);
        return card;
    }

    private void CreateRecoveryCheckpoint()
    {
        var target = GetExperienceTarget();
        if (target is null) { MessageBox.Show(this, "Connect the Switch SD card first.", "Recovery Center", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        try
        {
            var path = _experienceCheckpoint.Create(target, "Recovery Center checkpoint");
            _recoveryStatus.Text = $"Latest: {Path.GetFileName(path)}";
            SetStatus("●  CHECKPOINT CREATED", Theme.Green);
        }
        catch (Exception ex)
        {
            _logger.Error("Recovery checkpoint failed", ex);
            MessageBox.Show(this, ex.Message, "Recovery Center", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreRecoveryCheckpoint()
    {
        var target = GetExperienceTarget();
        if (target is null) { MessageBox.Show(this, "Connect the Switch SD card first.", "Recovery Center", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var checkpoints = _experienceCheckpoint.List();
        if (checkpoints.Count == 0) { MessageBox.Show(this, "No checkpoints are available.", "Recovery Center", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var picker = new CheckpointPickerDialog(checkpoints);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.Selected is null) return;
        if (MessageBox.Show(this, "This will replace only MewNX-managed configuration files from the selected checkpoint. It will not format the SD card or touch NAND.\n\nContinue?", "RESTORE CHECKPOINT", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try
        {
            _experienceCheckpoint.Restore(target, picker.Selected);
            _recoveryStatus.Text = $"Restored: {Path.GetFileName(picker.Selected)}";
            SetStatus("●  CHECKPOINT RESTORED", Theme.Green);
            _ = RefreshExperienceAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Checkpoint restore failed", ex);
            SetStatus("●  CHECKPOINT RESTORE FAILED", Theme.Red);
            MessageBox.Show(this, ex.Message, "Recovery Center", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenRecoveryDataFolder()
    {
        var paths = AppPaths.Create(_config);
        Directory.CreateDirectory(paths.DataDirectory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = paths.DataDirectory, UseShellExecute = true });
    }
}

internal sealed class CheckpointPickerDialog : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    public string? Selected => _list.SelectedItem as string;

    public CheckpointPickerDialog(IEnumerable<string> checkpoints)
    {
        Text = "MewSwitch — Restore Checkpoint";
        Width = 560; Height = 420; StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Surface; ForeColor = Theme.Text;
        foreach (var checkpoint in checkpoints) _list.Items.Add(checkpoint);
        _list.BackColor = Theme.Surface2; _list.ForeColor = Theme.Text; _list.Font = Theme.Mono(8.5f);
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        var ok = new Button { Text = "RESTORE SELECTED", Dock = DockStyle.Bottom, Height = 42, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "CANCEL", Dock = DockStyle.Bottom, Height = 34, DialogResult = DialogResult.Cancel };
        Controls.Add(_list); Controls.Add(cancel); Controls.Add(ok); AcceptButton = ok; CancelButton = cancel;
    }
}
