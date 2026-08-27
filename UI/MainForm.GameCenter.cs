using MewSwitchManager.Core;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private GameCenterService _gameCenter = null!;
    private GameCenterQueue _gameQueue = null!;
    private readonly Button _gamePickButton = new();
    private readonly Button _gameStageButton = new();
    private readonly Button _gameAddUrlButton = new();
    private readonly Button _gameProcessQueueButton = new();
    private readonly TextBox _gameUrl = new();
    private readonly ListView _gameQueueList = new();
    private readonly Label _gameFileLabel = new();
    private readonly Label _gameQueueStatus = new();
    private GameContentInfo? _gameContent;

    private Control BuildGameCenterSection()
    {
        _gameCenter ??= new GameCenterService(_logger);
        _gameQueue ??= new GameCenterQueue(_paths, _logger);
        var card = CreateCard();
        card.Height = 430;
        card.Padding = new Padding(16);

        var title = new Label { Dock = DockStyle.Top, Height = 28, Text = "GAME CENTER — DOWNLOAD QUEUE", ForeColor = Theme.Text, Font = Theme.Mono(11, FontStyle.Bold) };
        var info = new Label { Dock = DockStyle.Top, Height = 42, Text = "Contenido proporcionado por el usuario • descargas reanudables • procesamiento seguro • verificación antes de limpiar temporales.", ForeColor = Theme.Muted, Font = Theme.Mono(7.1f) };

        var urlRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, BackColor = Theme.Surface, Padding = new Padding(0, 4, 0, 4) };
        _gameUrl.Width = 510; _gameUrl.BackColor = Theme.Surface2; _gameUrl.ForeColor = Theme.Text; _gameUrl.BorderStyle = BorderStyle.FixedSingle; _gameUrl.PlaceholderText = "Direct URL to content you are authorized to download";
        StyleButton(_gameAddUrlButton, "ADD URL", Theme.Blue, 105);
        StyleButton(_gamePickButton, "LOCAL FILE", Theme.Blue, 105);
        StyleButton(_gameProcessQueueButton, "PROCESS QUEUE", Theme.Pink, 145);
        _gameAddUrlButton.Click += (_, _) => AddGameUrl();
        _gamePickButton.Click += async (_, _) => await SelectGameContentAsync();
        _gameProcessQueueButton.Click += async (_, _) => await ProcessGameQueueAsync();
        urlRow.Controls.Add(_gameUrl); urlRow.Controls.Add(_gameAddUrlButton); urlRow.Controls.Add(_gamePickButton); urlRow.Controls.Add(_gameProcessQueueButton);

        ConfigureGameQueueList();
        var queueHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0), BackColor = Theme.Surface };
        queueHost.Controls.Add(_gameQueueList);
        _gameQueueStatus.Dock = DockStyle.Bottom; _gameQueueStatus.Height = 24; _gameQueueStatus.ForeColor = Theme.Muted; _gameQueueStatus.Font = Theme.Mono(7); _gameQueueStatus.Text = "Queue persistent — no jobs yet.";

        _gameFileLabel.Dock = DockStyle.Bottom; _gameFileLabel.Height = 72; _gameFileLabel.Text = "LOCAL CONTENT\nNo file selected. The original source is never modified."; _gameFileLabel.ForeColor = Theme.Subtle; _gameFileLabel.Font = Theme.Mono(7.2f); _gameFileLabel.Padding = new Padding(8); _gameFileLabel.AutoEllipsis = true;

        _gameStageButton.Visible = false;
        card.Controls.Add(queueHost); card.Controls.Add(_gameQueueStatus); card.Controls.Add(_gameFileLabel); card.Controls.Add(urlRow); card.Controls.Add(info); card.Controls.Add(title);
        RefreshGameQueueList();
        return card;
    }

    private void ConfigureGameQueueList()
    {
        _gameQueueList.Dock = DockStyle.Fill; _gameQueueList.View = View.Details; _gameQueueList.FullRowSelect = true; _gameQueueList.HideSelection = false; _gameQueueList.BackColor = Theme.Surface2; _gameQueueList.ForeColor = Theme.Text; _gameQueueList.BorderStyle = BorderStyle.None; _gameQueueList.Font = Theme.UI(8);
        _gameQueueList.Columns.Add("NAME", 250); _gameQueueList.Columns.Add("STATE", 110); _gameQueueList.Columns.Add("PROGRESS", 120); _gameQueueList.Columns.Add("SOURCE", 420); _gameQueueList.Columns.Add("ERROR", 300);
        _gameQueue.Changed += RefreshGameQueueList;
    }

    private void RefreshGameQueueList()
    {
        if (InvokeRequired) { BeginInvoke(RefreshGameQueueList); return; }
        _gameQueueList.BeginUpdate();
        try
        {
            _gameQueueList.Items.Clear();
            foreach (var job in _gameQueue.Jobs)
            {
                var percent = job.TotalBytes is > 0 ? $"{job.BytesReceived * 100d / job.TotalBytes.Value:0.0}%" : job.BytesReceived > 0 ? $"{job.BytesReceived:N0} B" : "—";
                var item = new ListViewItem(job.Name); item.SubItems.Add(job.State.ToString().ToUpperInvariant()); item.SubItems.Add(percent); item.SubItems.Add(job.Source); item.SubItems.Add(job.Error ?? ""); _gameQueueList.Items.Add(item);
            }
            _gameQueueStatus.Text = _gameQueue.Jobs.Count == 0 ? "Queue persistent — no jobs yet." : $"{_gameQueue.Jobs.Count} persistent job(s). Incomplete downloads remain resumable.";
        }
        finally { _gameQueueList.EndUpdate(); }
    }

    private void AddGameUrl()
    {
        var url = _gameUrl.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) { MessageBox.Show(this, "Introduce una URL HTTP(S) directa a contenido que tengas derecho a descargar.", "Game Center", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        try
        {
            var name = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(name)) name = "remote-content";
            _gameQueue.AddDirectUrl(name, uri.ToString());
            _gameUrl.Clear();
            SetStatus("●  GAME DOWNLOAD QUEUED", Theme.Blue);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Game Center", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private async Task ProcessGameQueueAsync()
    {
        var pending = _gameQueue.Jobs.Where(x => x.State is DownloadJobState.Queued or DownloadJobState.Failed or DownloadJobState.Cancelled).ToList();
        if (pending.Count == 0) { MessageBox.Show(this, "No hay trabajos pendientes.", "Game Center", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (!TryGetAioTarget(out var root)) { MessageBox.Show(this, "No se detectó la microSD/almacenamiento de la Switch.", "Game Center", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        _operationCts = new CancellationTokenSource();
        try
        {
            _gameProcessQueueButton.Enabled = false;
            foreach (var job in pending)
            {
                _operationCts.Token.ThrowIfCancellationRequested();
                await _gameQueue.ProcessAsync(job, async prepared =>
                {
                    // Safe scope: stage verified user-provided content only. An installer adapter can consume this staged file.
                    var info = await _gameCenter.InspectAsync(prepared.Path, _operationCts.Token);
                    var result = await _gameCenter.StageAsync(info, root, _operationCts.Token);
                    _logger.Info($"Game Center staged {result.DestinationPath} after successful verification.");
                }, new Progress<DownloadProgress>(p =>
                {
                    _gameQueueStatus.Text = p.TotalBytes is > 0 ? $"{p.Phase}: {p.BytesReceived * 100d / p.TotalBytes.Value:0.0}% • {p.SpeedBytesPerSecond / 1024d / 1024d:0.0} MiB/s" : $"{p.Phase}: {p.BytesReceived:N0} bytes";
                }), _operationCts.Token);
            }
            SetStatus("●  GAME QUEUE COMPLETE", Theme.Green);
        }
        catch (OperationCanceledException) { SetStatus("●  GAME QUEUE CANCELLED", Theme.Amber); }
        catch (Exception ex) { SetStatus("●  GAME QUEUE FAILED", Theme.Red); _logger.Error("Game Center queue failed", ex); }
        finally { _gameProcessQueueButton.Enabled = true; _operationCts.Dispose(); _operationCts = null; RefreshGameQueueList(); }
    }

    private async Task SelectGameContentAsync()
    {
        using var dialog = new OpenFileDialog { Title = "MewNX — Select user-provided content", Filter = "Switch/Homebrew content|*.nsp;*.nsz;*.xci;*.xcz;*.nro;*.nca;*.zip|All files|*.*", CheckFileExists = true, Multiselect = false, RestoreDirectory = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _gamePickButton.Enabled = false; SetStatus("●  GAME CONTENT VERIFY", Theme.Blue);
            _gameContent = await _gameCenter.InspectAsync(dialog.FileName);
            _gameFileLabel.Text = $"READY / VERIFIED  •  {_gameContent.DisplayName}\nSIZE {_gameContent.SizeBytes:N0} bytes  •  TYPE {_gameContent.Extension}\nSHA-256 {_gameContent.Sha256}\nSource remains untouched.";
            _gameFileLabel.ForeColor = Theme.Green; SetStatus("●  GAME CONTENT READY", Theme.Green);
        }
        catch (Exception ex) { _gameContent = null; _gameFileLabel.Text = "PREFLIGHT FAILED\n" + ex.Message; _gameFileLabel.ForeColor = Theme.Red; SetStatus("●  GAME CONTENT BLOCKED", Theme.Red); _logger.Error("Game Center preflight failed", ex); }
        finally { _gamePickButton.Enabled = true; }
    }
}
