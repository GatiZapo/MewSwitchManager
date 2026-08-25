using MewSwitchManager.Core;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm : Form
{
    private readonly InstallationEngine _engine;
    private readonly AppLogger _logger;
    private readonly AppConfig _config;
    private readonly GitHubUpdateService _updates;

    private readonly NeonProgressBar _progress = new();
    private readonly StatusCard _rcm = new() { Heading = "SWITCH / RCM" };
    private readonly StatusCard _wsl = new() { Heading = "WINDOWS / WSL" };
    private readonly StatusCard _linux = new() { Heading = "LINUX IMAGE" };
    private readonly StatusCard _usb = new() { Heading = "TARGET USB" };
    private readonly StageList _stages = new();
    private readonly RichTextBox _log = new();
    private readonly ComboBox _diskSelector = new();
    private readonly Label _status = new();
    private readonly Label _updateStatus = new();
    private readonly Label _targetHint = new();
    private readonly Label _resumeTitle = new();
    private readonly Label _resumeDetail = new();
    private readonly Button _resumeAction = new();
    private readonly Button _refresh = new();
    private readonly Button _preflight = new();
    private readonly Button _download = new();
    private readonly Button _start = new();
    private readonly Button _cancel = new();
    private readonly Label _footer = new();
    private readonly Panel _sidebar = new();
    private readonly Panel _compactNav = new();
    private readonly TableLayoutPanel _content = new();
    private readonly Panel _scrollHost = new();
    private CancellationTokenSource? _operationCts;
    private bool _suppressDiskSelection;

    public static MainForm CreateDefault(AppPaths paths, AppLogger logger, AppConfig config)
        => new(new InstallationEngine(paths, config, logger), logger, config);

    private MainForm(InstallationEngine engine, AppLogger logger, AppConfig config)
    {
        _engine = engine;
        _logger = logger;
        _config = config;
        _updates = new GitHubUpdateService(new HttpClient(), logger, config);

        Text = "MewSwitch Manager";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 620);
        Size = new Size(1420, 900);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.UI(9.5f);
        KeyPreview = true;
        DoubleBuffered = true;

        BuildUi();
        EnableDarkTitleBar();

        _logger.Message += Logger_Message;
        _engine.StateChanged += Engine_StateChanged;
        Resize += (_, _) => ApplyResponsiveLayout();
        Shown += async (_, _) =>
        {
            await RefreshAsync();
            if (_config.Updates.CheckOnStartup)
                await CheckForUpdatesAsync();
        };
        FormClosing += (_, _) => _operationCts?.Cancel();
        KeyDown += MainForm_KeyDown;
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            _updateStatus.Text = "CHECKING GITHUB...";
            _updateStatus.ForeColor = Theme.Muted;
            var result = await _updates.CheckAsync();
            if (result.UpdateAvailable)
            {
                _updateStatus.Text = $"UPDATE AVAILABLE  //  {result.LatestVersion}";
                _updateStatus.ForeColor = Theme.Pink;
                if (!string.IsNullOrWhiteSpace(result.ReleaseUrl))
                {
                    _updateStatus.Cursor = Cursors.Hand;
                    _updateStatus.Click -= UpdateStatus_Click;
                    _updateStatus.Click += UpdateStatus_Click;
                    _updateStatus.Tag = result.ReleaseUrl;
                }
            }
            else
            {
                _updateStatus.Text = result.Error is null ? $"UP TO DATE  //  {result.CurrentVersion}" : "GITHUB CHECK UNAVAILABLE";
                _updateStatus.ForeColor = result.Error is null ? Theme.Green : Theme.Muted;
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Update UI check failed: {ex.Message}");
            _updateStatus.Text = "GITHUB CHECK FAILED";
            _updateStatus.ForeColor = Theme.Muted;
        }
    }

    private void UpdateStatus_Click(object? sender, EventArgs e)
    {
        if (_updateStatus.Tag is not string url || string.IsNullOrWhiteSpace(url)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { _logger.Warn($"Could not open release page: {ex.Message}"); }
    }
}
