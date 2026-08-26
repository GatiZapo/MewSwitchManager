using System.Diagnostics;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private UpdateInfo? _latestUpdate;
    private readonly Label _updateStatus = new();
    private readonly Label _updateVersion = new();
    private readonly RichTextBox _updateNotes = new();
    private readonly Button _updateCheck = new();
    private readonly Button _updateInstall = new();

    private void InitializeUpdateCenter()
    {
        _content.RowCount = Math.Max(_content.RowCount, 8);
        _content.Controls.Add(BuildUpdateCenterSection(), 0, 7);

        var nav = _sidebar.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if (nav is not null)
        {
            nav.Height = 360;
            var button = NavigationButton("06   UPDATE CENTER", false, 7);
            button.Width = 194;
            nav.Controls.Add(button);
        }

        var version = _updateService.GetCurrentVersion();
        ReplaceTextRecursive(_sidebar, "MANAGER  //  0.2 ALPHA", $"MANAGER  //  {version.ToUpperInvariant()}");
        ReplaceTextRecursive(_compactNav, "0.2 ALPHA", version.ToUpperInvariant());
    }

    private static void ReplaceTextRecursive(Control parent, string oldText, string newText)
    {
        foreach (Control child in parent.Controls)
        {
            if (string.Equals(child.Text, oldText, StringComparison.OrdinalIgnoreCase)) child.Text = newText;
            if (child.HasChildren) ReplaceTextRecursive(child, oldText, newText);
        }
    }

    private Control BuildUpdateCenterSection()
    {
        var card = CreateCard();
        card.Height = 285;
        card.Padding = new Padding(16);

        var title = new Label { Dock = DockStyle.Top, Height = 30, Text = "UPDATE CENTER", ForeColor = Theme.Text, Font = Theme.UI(13, FontStyle.Bold) };
        var subtitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "Checks GitHub Releases first, then the latest successful main CI build while the project is in development.",
            ForeColor = Theme.Muted,
            Font = Theme.UI(8.3f)
        };
        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 44, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            BackColor = Theme.Surface, Padding = new Padding(0, 4, 0, 0), Margin = Padding.Empty
        };
        StyleButton(_updateCheck, "CHECK FOR UPDATES", Theme.Blue, 150);
        StyleButton(_updateInstall, "UPDATE NOW", Theme.Pink, 120);
        _updateInstall.Enabled = false;
        _updateCheck.Click += async (_, _) => await CheckForUpdatesAsync();
        _updateInstall.Click += async (_, _) => await InstallUpdateAsync();
        actionRow.Controls.Add(_updateCheck);
        actionRow.Controls.Add(_updateInstall);

        _updateStatus.Text = "● READY TO CHECK";
        _updateStatus.Dock = DockStyle.Top;
        _updateStatus.Height = 27;
        _updateStatus.ForeColor = Theme.Muted;
        _updateStatus.Font = Theme.Mono(8.2f, FontStyle.Bold);

        _updateVersion.Text = "Installed: —    Latest: —";
        _updateVersion.Dock = DockStyle.Top;
        _updateVersion.Height = 24;
        _updateVersion.ForeColor = Theme.Text;
        _updateVersion.Font = Theme.Mono(8f);

        _updateNotes.Dock = DockStyle.Fill;
        _updateNotes.ReadOnly = true;
        _updateNotes.BorderStyle = BorderStyle.None;
        _updateNotes.BackColor = Color.FromArgb(4, 5, 8);
        _updateNotes.ForeColor = Theme.Muted;
        _updateNotes.Font = Theme.Mono(7.8f);
        _updateNotes.Text = "Click CHECK FOR UPDATES to query GitHub.";
        _updateNotes.Margin = new Padding(0, 5, 0, 0);

        card.Controls.Add(_updateNotes);
        card.Controls.Add(_updateVersion);
        card.Controls.Add(_updateStatus);
        card.Controls.Add(actionRow);
        card.Controls.Add(subtitle);
        card.Controls.Add(title);
        return card;
    }

    private async Task CheckForUpdatesAsync()
    {
        _updateCheck.Enabled = false;
        _updateInstall.Enabled = false;
        _updateStatus.Text = "● CHECKING GITHUB...";
        _updateStatus.ForeColor = Theme.Amber;

        try
        {
            var update = await _updateService.CheckAsync();
            _latestUpdate = update;
            _updateVersion.Text = string.IsNullOrWhiteSpace(update.LatestVersion)
                ? $"Installed: {update.CurrentVersion}    Latest: —"
                : $"Installed: {update.CurrentVersion}    Latest: {update.LatestVersion}";

            if (!string.IsNullOrWhiteSpace(update.ErrorMessage) && !update.IsDevelopmentUpdate)
            {
                _updateStatus.Text = "! UPDATE CHECK FAILED";
                _updateStatus.ForeColor = Theme.Red;
                _updateNotes.Text = update.ErrorMessage;
                _logger.Warn($"Update center: {update.ErrorMessage}");
                return;
            }

            if (update.IsAvailable)
            {
                _updateStatus.Text = update.IsDevelopmentUpdate
                    ? $"↑ DEVELOPMENT UPDATE AVAILABLE  //  {update.LatestCommitSha?[..Math.Min(7, update.LatestCommitSha.Length)]}"
                    : $"↑ UPDATE AVAILABLE  //  {update.LatestVersion}";
                _updateStatus.ForeColor = Theme.Pink;
                _updateInstall.Enabled = !string.IsNullOrWhiteSpace(update.AssetUrl);
                var notes = string.IsNullOrWhiteSpace(update.ReleaseNotes) ? "A newer build is available." : update.ReleaseNotes;
                if (update.IsDevelopmentUpdate && !string.IsNullOrWhiteSpace(update.LatestCommitUrl))
                    notes += Environment.NewLine + Environment.NewLine + $"Latest main commit: {update.LatestCommitMessage}";
                if (!string.IsNullOrWhiteSpace(update.ErrorMessage)) notes += Environment.NewLine + Environment.NewLine + update.ErrorMessage;
                _updateNotes.Text = notes;
            }
            else if (update.IsDevelopmentUpdate && !string.IsNullOrWhiteSpace(update.ErrorMessage))
            {
                _updateStatus.Text = "✓ DEVELOPMENT BUILD CHECKED";
                _updateStatus.ForeColor = Theme.Amber;
                _updateNotes.Text = update.ErrorMessage + Environment.NewLine + Environment.NewLine + (update.LatestCommitMessage ?? "");
            }
            else if (string.IsNullOrWhiteSpace(update.LatestVersion))
            {
                _updateStatus.Text = "✓ NO PUBLIC RELEASES / CI CURRENT";
                _updateStatus.ForeColor = Theme.Muted;
                _updateNotes.Text = "There is no public release yet. The latest successful main CI build was checked instead.";
            }
            else
            {
                _updateStatus.Text = "✓ MEWSWITCH MANAGER IS UP TO DATE";
                _updateStatus.ForeColor = Theme.Green;
                _updateNotes.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                    ? $"Release {update.TagName} is currently installed."
                    : update.ReleaseNotes;
            }
        }
        catch (OperationCanceledException)
        {
            _updateStatus.Text = "● UPDATE CHECK CANCELLED";
            _updateStatus.ForeColor = Theme.Amber;
        }
        catch (Exception ex)
        {
            _updateStatus.Text = "! UPDATE CHECK FAILED";
            _updateStatus.ForeColor = Theme.Red;
            _updateNotes.Text = ex.Message;
            _logger.Error($"Update center: {ex.Message}");
        }
        finally
        {
            _updateCheck.Enabled = true;
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (_latestUpdate is null || !_latestUpdate.IsAvailable) return;
        if (string.IsNullOrWhiteSpace(_latestUpdate.AssetUrl))
        {
            if (!string.IsNullOrWhiteSpace(_latestUpdate.ReleaseUrl))
                Process.Start(new ProcessStartInfo(_latestUpdate.ReleaseUrl) { UseShellExecute = true });
            return;
        }

        _updateInstall.Enabled = false;
        _updateCheck.Enabled = false;
        _updateStatus.Text = "↓ DOWNLOADING UPDATE...";
        _updateStatus.ForeColor = Theme.Blue;

        var started = await _updateService.DownloadAndInstallAsync(_latestUpdate);
        if (started)
        {
            _updateStatus.Text = "✓ UPDATE DOWNLOADED. RESTARTING...";
            _updateStatus.ForeColor = Theme.Green;
            await Task.Delay(500);
            Application.Exit();
            return;
        }

        _updateStatus.Text = "✕ UPDATE FAILED. CURRENT INSTALLATION WAS NOT REPLACED.";
        _updateStatus.ForeColor = Theme.Red;
        _updateCheck.Enabled = true;
    }

    private async Task CheckForUpdatesOnStartupAsync() => await CheckForUpdatesAsync();
}
