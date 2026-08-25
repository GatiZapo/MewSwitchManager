using MewSwitchManager.Core;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm : Form
{
    private readonly InstallationEngine _engine;
    private readonly AppLogger _logger;
    private readonly AppConfig _config;
    private readonly UpdateService _updateService;

    private readonly NeonProgressBar _progress = new();
    private readonly StatusCard _rcm = new() { Heading = "SWITCH / RCM" };
    private readonly StatusCard _wsl = new() { Heading = "WINDOWS / WSL" };
    private readonly StatusCard _linux = new() { Heading = "LINUX IMAGE" };
    private readonly StatusCard _usb = new() { Heading = "TARGET USB" };
    private readonly StageList _stages = new();
    private readonly RichTextBox _log = new();
    private readonly ComboBox _diskSelector = new();
    private readonly Label _status = new();
    private readonly Label _targetHint = new();
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
        _updateService = new UpdateService(logger);

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
        InitializeUpdateCenter();
        ApplyVisualPolish();
        EnableDarkTitleBar();

        _logger.Message += Logger_Message;
        _engine.StateChanged += Engine_StateChanged;
        Resize += (_, _) => ApplyResponsiveLayout();
        Shown += async (_, _) =>
        {
            ApplyVisualPolish();
            await RefreshAsync();
            await CheckForUpdatesOnStartupAsync();
        };
        FormClosing += (_, _) => _operationCts?.Cancel();
        KeyDown += MainForm_KeyDown;
    }

}
