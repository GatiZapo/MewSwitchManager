using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Theme.Background,
            Padding = new Padding(18),
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 216));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        BuildSidebar();
        root.Controls.Add(_sidebar, 0, 0);
        root.SetRowSpan(_sidebar, 2);

        _scrollHost.Dock = DockStyle.Fill;
        _scrollHost.AutoScroll = true;
        _scrollHost.BackColor = Theme.Background;
        _scrollHost.Padding = new Padding(18, 0, 2, 0);
        root.Controls.Add(_scrollHost, 1, 1);

        _content.Dock = DockStyle.Top;
        _content.AutoSize = true;
        _content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _content.ColumnCount = 1;
        _content.RowCount = 7;
        for (var i = 0; i < 7; i++) _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.BackColor = Theme.Background;
        _content.Margin = Padding.Empty;
        _content.Padding = Padding.Empty;
        _content.RowStyles.Clear();
        _scrollHost.Controls.Add(_content);
        _scrollHost.Resize += (_, _) => FitContentWidth();

        _content.Controls.Add(BuildHeader(), 0, 0);
        _content.Controls.Add(BuildTargetSection(), 0, 1);
        _content.Controls.Add(BuildResumeSection(), 0, 2);
        _content.Controls.Add(BuildHealthSection(), 0, 3);
        _content.Controls.Add(BuildStagesSection(), 0, 4);
        _content.Controls.Add(BuildLogSection(), 0, 5);
        _content.Controls.Add(BuildFooter(), 0, 6);

        BuildCompactNav(root);
        FitContentWidth();
        ApplyResponsiveLayout();
    }

    private void BuildSidebar()
    {
        _sidebar.BackColor = Theme.Surface;
        _sidebar.Dock = DockStyle.Fill;
        _sidebar.Padding = new Padding(14);
        _sidebar.Margin = Padding.Empty;
        _sidebar.Paint += (_, e) => Theme.Round(e.Graphics, new Rectangle(0, 0, _sidebar.Width - 1, _sidebar.Height - 1), 16, Theme.Surface, Theme.Border);

        var logo = new Label
        {
            Dock = DockStyle.Top,
            Height = 82,
            Text = "MEW\nSWITCH",
            ForeColor = Theme.Pink,
            Font = Theme.UI(24, FontStyle.Bold),
            Padding = new Padding(8, 4, 0, 0),
            AutoSize = false
        };
        var version = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "MANAGER  //  0.3 ALPHA",
            ForeColor = Theme.Muted,
            Font = Theme.Mono(8, FontStyle.Bold),
            Padding = new Padding(8, 0, 0, 0)
        };

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 230,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 16, 0, 0),
            Margin = Padding.Empty
        };
        nav.Controls.Add(NavigationButton("01   DASHBOARD", true, 0));
        nav.Controls.Add(NavigationButton("02   USB WORKFLOW", false, 1));
        nav.Controls.Add(NavigationButton("03   CHECKPOINTS", false, 2));
        nav.Controls.Add(NavigationButton("04   DIAGNOSTICS", false, 3));
        nav.Controls.Add(NavigationButton("05   SAFETY", false, 1));
        foreach (Control control in nav.Controls) control.Width = 180;

        var safety = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(8, 20, 8, 0) };
        safety.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "SAFETY ENGINE",
            ForeColor = Theme.Green,
            Font = Theme.Mono(8, FontStyle.Bold)
        });
        safety.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 150,
            Text = "● Windows system disks blocked\n● USB-only target selection\n● Saved USB identity re-checked\n● Identity re-check before clean\n● Identity re-check before write\n● Image verification re-checked before flash\n● Download progress is resumable",
            ForeColor = Theme.Muted,
            Font = Theme.UI(8.5f),
            Padding = new Padding(0, 8, 0, 0)
        });

        var footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Text = "SYSTEM ONLINE\nMEW // GLITCH",
            ForeColor = Theme.Muted,
            Font = Theme.Mono(7.5f),
            Padding = new Padding(8, 4, 0, 0)
        };

        _sidebar.Controls.Add(footer);
        _sidebar.Controls.Add(safety);
        _sidebar.Controls.Add(nav);
        _sidebar.Controls.Add(version);
        _sidebar.Controls.Add(logo);
    }

    private void BuildCompactNav(TableLayoutPanel root)
    {
        _compactNav.Dock = DockStyle.Top;
        _compactNav.Height = 52;
        _compactNav.BackColor = Theme.Surface;
        _compactNav.Padding = new Padding(10, 8, 10, 8);
        _compactNav.Visible = false;
        _compactNav.Paint += (_, e) => Theme.Round(e.Graphics, new Rectangle(0, 0, _compactNav.Width - 1, _compactNav.Height - 1), 12, Theme.Surface, Theme.Border);

        var brand = new Label { Text = "MEW / SWITCH", Dock = DockStyle.Left, Width = 180, ForeColor = Theme.Pink, Font = Theme.Mono(10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        var mode = new Label { Text = "0.3 ALPHA", Dock = DockStyle.Right, Width = 100, ForeColor = Theme.Muted, Font = Theme.Mono(8), TextAlign = ContentAlignment.MiddleRight };
        _compactNav.Controls.Add(mode);
        _compactNav.Controls.Add(brand);

        root.Controls.Add(_compactNav, 0, 0);
        root.SetColumnSpan(_compactNav, 2);
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 112, BackColor = Theme.Background, Margin = new Padding(0, 0, 0, 12) };
        var title = new Label
        {
            Text = "Installation Control Center",
            Dock = DockStyle.Top,
            Height = 42,
            ForeColor = Theme.Text,
            Font = Theme.UI(27, FontStyle.Bold),
            AutoEllipsis = true
        };
        var subtitle = new Label
        {
            Text = "GUARDED WORKFLOW // STATE PERSISTENCE // SAFE USB TARGETING",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Theme.Muted,
            Font = Theme.Mono(8.5f, FontStyle.Bold),
            AutoEllipsis = true
        };
        var statusPill = new Panel { Dock = DockStyle.Right, Width = 250, BackColor = Theme.Background, Padding = new Padding(0, 4, 0, 0) };
        _status.Text = "●  SYSTEM READY";
        _status.ForeColor = Theme.Green;
        _status.Font = Theme.Mono(9, FontStyle.Bold);
        _status.Dock = DockStyle.Top;
        _status.Height = 32;
        _status.TextAlign = ContentAlignment.MiddleRight;

        _updateStatus.Text = "GITHUB UPDATE CHECK PENDING";
        _updateStatus.ForeColor = Theme.Muted;
        _updateStatus.Font = Theme.Mono(7.5f, FontStyle.Bold);
        _updateStatus.Dock = DockStyle.Top;
        _updateStatus.Height = 24;
        _updateStatus.TextAlign = ContentAlignment.MiddleRight;
        _updateStatus.AutoEllipsis = true;

        statusPill.Controls.Add(_updateStatus);
        statusPill.Controls.Add(_status);

        panel.Controls.Add(statusPill);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildTargetSection()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Surface,
            Margin = Padding.Empty,
            Padding = new Padding(18)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label { Text = "TARGET DEVICE", Dock = DockStyle.Fill, ForeColor = Theme.Text, Font = Theme.UI(14, FontStyle.Bold) };
        layout.Controls.Add(heading, 0, 0);

        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Surface, Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));

        _diskSelector.Dock = DockStyle.Fill;
        _diskSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _diskSelector.FlatStyle = FlatStyle.Flat;
        _diskSelector.BackColor = Theme.Surface2;
        _diskSelector.ForeColor = Theme.Text;
        _diskSelector.Font = Theme.UI(9.5f);
        _diskSelector.Margin = new Padding(0, 4, 10, 4);
        _diskSelector.AccessibleName = "USB target disk";
        _diskSelector.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressDiskSelection) return;
            if (_diskSelector.SelectedItem is DiskInfo disk)
            {
                _engine.SelectDisk(disk);
                UpdateUsbCard();
            }
        };
        row.Controls.Add(_diskSelector, 0, 0);

        StyleButton(_refresh, "REFRESH", Theme.Blue, 100);
        _refresh.Click += async (_, _) => await RefreshAsync();
        row.Controls.Add(_refresh, 1, 0);

        StyleButton(_cancel, "CANCEL", Theme.Amber, 100);
        _cancel.Enabled = false;
        _cancel.Click += (_, _) => _operationCts?.Cancel();
        row.Controls.Add(_cancel, 2, 0);
        layout.Controls.Add(row, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 4, 0, 0),
            Margin = Padding.Empty
        };
        StyleButton(_preflight, "PREFLIGHT", Theme.Blue, 116);
        StyleButton(_download, "DOWNLOAD LINUX", Theme.Pink, 142);
        StyleButton(_start, "START USB WRITE", Theme.Red, 142);
        _preflight.Margin = new Padding(0, 0, 8, 0);
        _download.Margin = new Padding(0, 0, 8, 0);
        _start.Margin = Padding.Empty;
        _preflight.Click += async (_, _) => await RunPreflightAsync();
        _download.Click += async (_, _) => await RunDownloadAsync();
        _start.Click += async (_, _) => await StartInstallationAsync();
        actions.Controls.Add(_preflight);
        actions.Controls.Add(_download);
        actions.Controls.Add(_start);
        layout.Controls.Add(actions, 0, 2);

        _targetHint.Text = "No USB target selected.";
        _targetHint.Dock = DockStyle.Fill;
        _targetHint.ForeColor = Theme.Muted;
        _targetHint.Font = Theme.UI(8.5f);
        _targetHint.AutoEllipsis = true;
        layout.Controls.Add(_targetHint, 0, 3);

        card.Controls.Add(layout);
        card.Height = 180;
        return card;
    }

    private Control BuildResumeSection()
    {
        var card = CreateCard();
        card.Height = 112;
        card.Padding = new Padding(16);

        var actionHost = new Panel { Dock = DockStyle.Right, Width = 188, Padding = new Padding(10, 12, 0, 0), BackColor = Theme.Surface };
        StyleButton(_resumeAction, "MARK CHECKPOINT", Theme.Green, 174);
        _resumeAction.Click += (_, _) => MarkResumeCheckpoint();
        actionHost.Controls.Add(_resumeAction);

        _resumeTitle.Dock = DockStyle.Top;
        _resumeTitle.Height = 28;
        _resumeTitle.Text = "RESUME ENGINE // INITIALIZING";
        _resumeTitle.ForeColor = Theme.Pink;
        _resumeTitle.Font = Theme.Mono(10, FontStyle.Bold);

        _resumeDetail.Dock = DockStyle.Fill;
        _resumeDetail.Text = "Loading persisted installation state...";
        _resumeDetail.ForeColor = Theme.Muted;
        _resumeDetail.Font = Theme.UI(8.5f);
        _resumeDetail.AutoEllipsis = true;
        _resumeDetail.Padding = new Padding(0, 4, 8, 0);

        card.Controls.Add(_resumeDetail);
        card.Controls.Add(_resumeTitle);
        card.Controls.Add(actionHost);
        return card;
    }

    private Control BuildHealthSection()
    {
        var host = new Panel { Dock = DockStyle.Fill, Height = 300, BackColor = Theme.Background, Margin = new Padding(0, 0, 0, 12) };
        var cards = new TableLayoutPanel { Dock = DockStyle.Top, Height = 94, ColumnCount = 4, RowCount = 1, BackColor = Theme.Background, Margin = Padding.Empty };
        for (var i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        var all = new[] { _rcm, _wsl, _linux, _usb };
        for (var i = 0; i < all.Length; i++)
        {
            all[i].Dock = DockStyle.Fill;
            all[i].Margin = new Padding(i == 0 ? 0 : 5, 0, i == 3 ? 0 : 5, 0);
            cards.Controls.Add(all[i], i, 0);
        }
        host.Controls.Add(cards);

        _progress.Dock = DockStyle.Top;
        _progress.Height = 126;
        _progress.Margin = new Padding(0, 12, 0, 0);
        host.Controls.Add(_progress);
        return host;
    }

    private Control BuildStagesSection()
    {
        var card = CreateCard();
        _stages.Dock = DockStyle.Fill;
        _stages.Margin = Padding.Empty;
        _stages.Height = 230;
        card.Height = 230;
        card.Padding = Padding.Empty;
        card.Controls.Add(_stages);
        return card;
    }

    private Control BuildLogSection()
    {
        var card = CreateCard();
        card.Height = 300;
        card.Padding = new Padding(16);

        var title = new Label { Dock = DockStyle.Top, Height = 30, Text = "LIVE OPERATION LOG // TRACE", ForeColor = Theme.Text, Font = Theme.UI(13, FontStyle.Bold) };
        var meta = new Label { Dock = DockStyle.Top, Height = 24, Text = "Persistent state, hardware probes and destructive gates are logged here.", ForeColor = Theme.Muted, Font = Theme.UI(8.5f) };
        _log.Dock = DockStyle.Fill;
        _log.ReadOnly = true;
        _log.BorderStyle = BorderStyle.None;
        _log.BackColor = Color.FromArgb(4, 5, 8);
        _log.ForeColor = Theme.Blue;
        _log.Font = Theme.Mono(8.5f);
        _log.ScrollBars = RichTextBoxScrollBars.Vertical;
        _log.DetectUrls = false;
        _log.Margin = new Padding(0, 8, 0, 0);
        _log.AccessibleName = "Operation log";
        card.Controls.Add(_log);
        card.Controls.Add(meta);
        card.Controls.Add(title);
        return card;
    }

    private Control BuildFooter()
    {
        var bar = new Panel { Dock = DockStyle.Fill, Height = 48, BackColor = Theme.Background, Padding = new Padding(2, 10, 2, 0) };
        _footer.Text = "STATE AUTO-SAVED  •  RESUME SAFE  •  USB IDENTITY LOCKED  •  DESTRUCTIVE ACTIONS REQUIRE CONFIRMATION";
        _footer.Dock = DockStyle.Fill;
        _footer.ForeColor = Theme.Muted;
        _footer.Font = Theme.Mono(7.5f);
        _footer.AutoEllipsis = true;
        bar.Controls.Add(_footer);
        return bar;
    }

    private static Panel CreateCard()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(1)
        };
        panel.Paint += (_, e) => Theme.Round(e.Graphics, new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 14, Theme.Surface, Theme.Border);
        return panel;
    }

    private Button NavigationButton(string text, bool active, int sectionIndex)
    {
        var button = new Button
        {
            Text = text,
            Width = 180,
            Height = 42,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = active ? Theme.Surface2 : Color.Transparent,
            ForeColor = active ? Theme.Text : Theme.Muted,
            Font = Theme.Mono(8, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = true
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.Surface2;
        button.Click += (_, _) => ScrollToSection(sectionIndex);
        return button;
    }

    private void ScrollToSection(int index)
    {
        if (index < 0 || index >= _content.Controls.Count) return;
        var control = _content.Controls[index];
        _scrollHost.AutoScrollPosition = new Point(0, Math.Max(0, control.Top - 8));
        control.Focus();
    }

    private static void StyleButton(Button button, string text, Color accent, int width)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 40;
        button.MinimumSize = new Size(width, 40);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = accent;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(34, 22, 40);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(45, 28, 50);
        button.BackColor = Theme.Surface2;
        button.ForeColor = Theme.Text;
        button.Font = Theme.Mono(8, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.TabStop = true;
        button.AccessibleName = text;
    }

}
