using System.Runtime.InteropServices;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private bool ConfirmDestructiveWrite(DiskInfo disk)
    {
        using var dialog = new Form
        {
            Text = "Confirm USB write",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(600, 230),
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            Font = Theme.UI(9.5f)
        };

        var label = new Label
        {
            Text = $"This is the final destructive gate.\n\nType exactly:\nWRITE DISK {disk.Number}",
            Dock = DockStyle.Top,
            Height = 116,
            ForeColor = Theme.Text,
            Padding = new Padding(18, 14, 18, 0)
        };
        var input = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 32,
            Margin = new Padding(18),
            BackColor = Theme.Surface2,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.Mono(10)
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(10),
            BackColor = Theme.Background
        };
        var confirm = new Button { Text = "WRITE USB", DialogResult = DialogResult.OK, Width = 120, Height = 38 };
        var cancel = new Button { Text = "CANCEL", DialogResult = DialogResult.Cancel, Width = 100, Height = 38 };
        StyleButton(confirm, "WRITE USB", Theme.Red, 120);
        StyleButton(cancel, "CANCEL", Theme.Muted, 100);
        buttons.Controls.Add(confirm);
        buttons.Controls.Add(cancel);
        dialog.Controls.Add(buttons);
        dialog.Controls.Add(input);
        dialog.Controls.Add(label);
        dialog.AcceptButton = confirm;
        dialog.CancelButton = cancel;
        dialog.Shown += (_, _) => input.Focus();

        var result = dialog.ShowDialog(this);
        return result == DialogResult.OK && string.Equals(input.Text.Trim(), $"WRITE DISK {disk.Number}", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyResponsiveLayout()
    {
        if (WindowState == FormWindowState.Minimized) return;
        _sidebar.Visible = true;
        _compactNav.Visible = false;
        if (ParentLayout(out var root))
        {
            root.ColumnStyles[0].Width = 216;
            root.ColumnStyles[1].Width = 100;
            if (root.RowStyles.Count > 0) root.RowStyles[0].Height = 112;
        }
        FitContentWidth();
    }

    private bool ParentLayout(out TableLayoutPanel root)
    {
        root = Controls.OfType<TableLayoutPanel>().FirstOrDefault()!;
        return root is not null;
    }

    private void FitContentWidth()
    {
        if (_scrollHost.ClientSize.Width <= 0) return;
        _content.Width = Math.Max(560, _scrollHost.ClientSize.Width - _scrollHost.Padding.Horizontal - 4);
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            e.Handled = true;
            _ = RefreshAsync();
        }
        else if (e.KeyCode == Keys.Escape && _operationCts is not null)
        {
            e.Handled = true;
            _operationCts.Cancel();
        }
    }

    private void Engine_StateChanged() => InvokeIfNeeded(UpdateUi);

    private void Logger_Message(string line) => InvokeIfNeeded(() =>
    {
        _log.AppendText(line + Environment.NewLine);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    });

    private void SetStatus(string text, Color color)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(text, color)); return; }
        _status.Text = text;
        _status.ForeColor = color;
        _aioActivityState.Text = text.Replace("●", "").Trim();
        _aioActivityState.ForeColor = color;
    }

    private void InvokeIfNeeded(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(action); else action();
    }

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var v = Math.Max(0, (double)value);
        var i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.0} {units[i]}";
    }

    private static string FormatSpeed(double value) => value <= 0 ? "—" : FormatBytes((long)value) + "/s";
    private static string FormatEta(TimeSpan? eta) => eta is null ? "ETA —" : $"ETA {eta.Value:hh\\:mm\\:ss}";

    private void EnableDarkTitleBar()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var useDark = 20;
            var value = 1;
            DwmSetWindowAttribute(Handle, useDark, ref value, sizeof(int));
        }
        catch { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
