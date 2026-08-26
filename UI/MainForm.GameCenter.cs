using MewSwitchManager.Core;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private GameCenterService _gameCenter = null!;
    private readonly Button _gamePickButton = new();
    private readonly Button _gameStageButton = new();
    private readonly Label _gameFileLabel = new();
    private GameContentInfo? _gameContent;

    private Control BuildGameCenterSection()
    {
        _gameCenter ??= new GameCenterService(_logger);
        var card = CreateCard();
        card.Height = 238;
        card.Padding = new Padding(16);

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "GAME CENTER",
            ForeColor = Theme.Text,
            Font = Theme.Mono(11, FontStyle.Bold)
        };
        var info = new Label
        {
            Dock = DockStyle.Top,
            Height = 50,
            Text = "User-provided content only • SHA-256 preflight • atomic staging • installer-ready handoff\nMewNX does not scrape or distribute unauthorised game dumps.",
            ForeColor = Theme.Muted,
            Font = Theme.Mono(7.1f)
        };
        _gameFileLabel.Dock = DockStyle.Fill;
        _gameFileLabel.Text = "NO CONTENT SELECTED\n\nSelect an NSP / NSZ / XCI / XCZ / NRO / ZIP file to inspect it before staging.";
        _gameFileLabel.ForeColor = Theme.Subtle;
        _gameFileLabel.Font = Theme.Mono(7.8f);
        _gameFileLabel.TextAlign = ContentAlignment.MiddleLeft;
        _gameFileLabel.AutoEllipsis = true;
        _gameFileLabel.Padding = new Padding(10, 8, 10, 8);

        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 8, 0, 0),
            Margin = Padding.Empty
        };
        StyleButton(_gamePickButton, "SELECT / VERIFY", Theme.Blue, 170);
        StyleButton(_gameStageButton, "STAGE TO SD", Theme.Pink, 150);
        _gameStageButton.Enabled = false;
        _gamePickButton.Click += async (_, _) => await SelectGameContentAsync();
        _gameStageButton.Click += async (_, _) => await StageGameContentAsync();
        row.Controls.Add(_gamePickButton);
        row.Controls.Add(_gameStageButton);

        card.Controls.Add(_gameFileLabel);
        card.Controls.Add(row);
        card.Controls.Add(info);
        card.Controls.Add(title);
        return card;
    }

    private async Task SelectGameContentAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "MewNX — Select user-provided content",
            Filter = "Switch/Homebrew content|*.nsp;*.nsz;*.xci;*.xcz;*.nro;*.nca;*.zip|All files|*.*",
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            _gamePickButton.Enabled = false;
            _gameStageButton.Enabled = false;
            SetStatus("●  GAME CONTENT VERIFY", Theme.Blue);
            _gameContent = await _gameCenter.InspectAsync(dialog.FileName);
            _gameFileLabel.Text =
                $"READY / VERIFIED\n\nFILE     {_gameContent.DisplayName}\nSIZE     {_gameContent.SizeBytes:N0} bytes\nTYPE     {_gameContent.Extension}\nSHA-256  {_gameContent.Sha256}\n\nSource remains untouched. MewNX will verify the hash again after staging.";
            _gameFileLabel.ForeColor = Theme.Green;
            _gameStageButton.Enabled = true;
            SetStatus("●  GAME CONTENT READY", Theme.Green);
        }
        catch (Exception ex)
        {
            _gameContent = null;
            _gameFileLabel.Text = "PREFLIGHT FAILED\n\n" + ex.Message;
            _gameFileLabel.ForeColor = Theme.Red;
            SetStatus("●  GAME CONTENT BLOCKED", Theme.Red);
            _logger.Error("Game Center preflight failed", ex);
        }
        finally { _gamePickButton.Enabled = true; }
    }

    private async Task StageGameContentAsync()
    {
        if (_gameContent is null) return;
        if (!TryGetAioTarget(out var root))
        {
            MessageBox.Show(this, "No se detectó la microSD/almacenamiento de la Switch.", "Game Center", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(this,
            "MewNX will copy the verified content to MewNX/Incoming on the selected SD card.\n\nThe source file will not be deleted or modified.\n\nAfter staging, use a supported installer adapter (for example DBI/Awoo) to install it.",
            "STAGE CONTENT", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        _operationCts = new CancellationTokenSource();
        try
        {
            _gameStageButton.Enabled = false;
            SetStatus("●  STAGING CONTENT", Theme.Pink);
            var result = await _gameCenter.StageAsync(_gameContent, root, _operationCts.Token);
            _gameFileLabel.Text += $"\n\nSTAGED\nDEST     {result.DestinationPath}\nHASH     {result.Sha256}";
            _gameFileLabel.ForeColor = Theme.Green;
            SetStatus("●  CONTENT STAGED / VERIFIED", Theme.Green);
            MessageBox.Show(this, result.Message + "\n\n" + result.DestinationPath, "Game Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { SetStatus("●  STAGING CANCELLED", Theme.Amber); }
        catch (Exception ex)
        {
            SetStatus("●  STAGING FAILED", Theme.Red);
            _logger.Error("Game Center staging failed", ex);
            MessageBox.Show(this, ex.Message, "Game Center", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _gameStageButton.Enabled = _gameContent is not null;
            _operationCts.Dispose();
            _operationCts = null;
        }
    }
}
