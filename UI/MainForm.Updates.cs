using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private readonly UpdateService _updateService;
    private UpdateInfo? _latestUpdate;
    private readonly Label _updateStatus = new();
    private readonly Label _updateVersion = new();
    private readonly RichTextBox _updateNotes = new();
    private readonly Button _updateCheck = new();
    private readonly Button _updateInstall = new();
    private readonly Panel _updateCard = new();

    private Control BuildUpdateCenterSection()
    {
        var card = CreateCard();
        card.Height = 270;
        card.Padding = new Padding(16);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "UPDATE CENTER",
            ForeColor = Theme.Text,
            Font = Theme.UI(13, FontStyle.Bold)
        };
        var subtitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "Checks the official GitHub Releases channel for MewSwitch Manager updates.",
            ForeColor = Theme.Muted,
            Font = Theme.UI(8.3f)
        };

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 4, 0, 0),
            Margin = Padding.Empty
        };
        StyleButton(_updateCheck, "CHECK FOR UPDATES", Theme.Blue, 150);
        StyleButton(_updateInstall, "UPDATE NOW", Theme.Pink, 120);
        _updateInstall.Enabled = false;
        _updateCheck.Click += async (_, _) => await CheckForUpdatesAsync();
        _updateInstall.Click += async (_, _) => await InstallUpdateAsync();
        actionRow.Controls.Add(_updateCheck);
        actionRow.Controls.Add(_updateInstall);

        _updateStatus.Text = "● Checking status has not been run yet.";
        _updateStatus.Dock = DockStyle.Top;
        _updateStatus.Height = 27;
        _updateStatus.ForeColor = Theme.Muted;
        _updateStatus.Font = Theme.Mono(8.2f, FontStyle.Bold);

        _updateVersion.Text = "Installed version: —";
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
        _updateNotes.Text = "Release notes will appear here when a release is found.";
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
        _updateStatus.Text = "● Checking GitHub Releases...";
        _updateStatus.ForeColor = Theme.Amber;

        try
        {
            var update = await _updateService.CheckAsync();
            _latestUpdate = update;
            _updateVersion.Text = $"Installed: {update.CurrentVersion}    Latest: {(string.IsNullOrWhiteSpace(update.LatestVersion) ? "no public release" : update.LatestVersion)}";

            if (update.IsAvailable)
            {
                _updateStatus.Text = $"↑ UPDATE AVAILABLE  //  {update.LatestVersion}";
                _updateStatus.ForeColor = Theme.Pink;
                _updateInstall.Enabled = !string.IsNullOrWhiteSpace(update.AssetUrl);
                _updateNotes.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes) ? "A new release is available." : update.ReleaseNotes;
            }
            else
            {
                _updateStatus.Text = string.IsNullOrWhiteSpace(update.LatestVersion)
                    ? "● No public GitHub Release exists yet."
                    : "✓ MewSwitch Manager is up to date.";
                _updateStatus.ForeColor = Theme.Green;
                _updateNotes.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes) ? "" : update.ReleaseNotes;
            }
        }
        catch (Exception ex)
        {
            _updateStatus.Text = "! Update check failed — see operation log.";
            _updateStatus.ForeColor = Theme.Red;
            _logger.Warn($"Update center: {ex.Message}");
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
        _updateStatus.Text = "↓ Downloading update...";
        _updateStatus.ForeColor = Theme.Blue;

        var started = await _updateService.DownloadAndInstallAsync(_latestUpdate);
        if (started)
        {
            _updateStatus.Text = "✓ Update downloaded. Restarting MewSwitch Manager...";
            _updateStatus.ForeColor = Theme.Green;
            await Task.Delay(500);
            Application.Exit();
            return;
        }

        _updateStatus.Text = "✕ Update failed. The current installation was not replaced.";
        _updateStatus.ForeColor = Theme.Red;
        _updateCheck.Enabled = true;
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        await CheckForUpdatesAsync();
    }
}
