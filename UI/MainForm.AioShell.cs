using System.Runtime.InteropServices;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private readonly Dictionary<string, Panel> _aioPages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Panel _aioActivity = new();
    private readonly Label _aioActivityTitle = new();
    private readonly Label _aioActivityState = new();

    private void InitializeAioShell()
    {
        var root = Controls.OfType<TableLayoutPanel>().FirstOrDefault();
        if (root is null) return;
        BuildAioActivityBar(root);
        RebrandExistingUi();
        BuildAioNavigation();
        BuildAioPages();
    }

    private void BuildAioActivityBar(TableLayoutPanel root)
    {
        root.RowStyles.Clear();
        root.RowCount = 2;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _aioActivity.Dock = DockStyle.Fill;
        _aioActivity.BackColor = Theme.Surface;
        _aioActivity.Margin = new Padding(14, 0, 14, 10);
        _aioActivity.Padding = new Padding(14, 10, 14, 10);
        _aioActivity.Paint += (_, e) => Theme.TechnicalFrame(e.Graphics,
            new Rectangle(0, 0, _aioActivity.Width, _aioActivity.Height), Theme.BorderStrong);

        _aioActivityTitle.Text = "ACTIVE OPERATION";
        _aioActivityTitle.Dock = DockStyle.Top;
        _aioActivityTitle.Height = 22;
        _aioActivityTitle.ForeColor = Theme.Text;
        _aioActivityTitle.Font = Theme.Mono(9.2f, FontStyle.Bold);

        _aioActivityState.Text = "READY — no operation running";
        _aioActivityState.Dock = DockStyle.Right;
        _aioActivityState.Width = 300;
        _aioActivityState.TextAlign = ContentAlignment.MiddleRight;
        _aioActivityState.ForeColor = Theme.Green;
        _aioActivityState.Font = Theme.Mono(7.8f, FontStyle.Bold);

        if (_progress.Parent is not null) _progress.Parent.Controls.Remove(_progress);
        _progress.Dock = DockStyle.Fill;
        _progress.Margin = Padding.Empty;
        _aioActivity.Controls.Add(_progress);
        _aioActivity.Controls.Add(_aioActivityState);
        _aioActivity.Controls.Add(_aioActivityTitle);
        root.Controls.Add(_aioActivity, 1, 0);
    }

    private void BuildAioNavigation()
    {
        var nav = _sidebar.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if (nav is null) return;
        nav.Controls.Clear();
        nav.Height = 380;
        nav.Padding = new Padding(0, 14, 0, 0);
        AddAioNavButton(nav, "01   HOME", "Home");
        AddAioNavButton(nav, "02   INSTALL", "Install");
        AddAioNavButton(nav, "03   SWITCH TOOLS", "Switch Tools");
        AddAioNavButton(nav, "04   EMULATION", "Emulation");
        AddAioNavButton(nav, "05   GAME CENTER", "Game Center");
        AddAioNavButton(nav, "06   RECOVERY", "Recovery");
        AddAioNavButton(nav, "07   DIAGNOSTICS", "Diagnostics");
        AddAioNavButton(nav, "08   UPDATES", "Updates");
    }

    private void AddAioNavButton(FlowLayoutPanel nav, string text, string page)
    {
        var button = new Button
        {
            Text = text,
            Width = 194,
            Height = 40,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Theme.Muted,
            Font = Theme.Mono(7.8f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Tag = page,
            TabStop = true
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => ShowAioPage(page);
        nav.Controls.Add(button);
    }

    private void BuildAioPages()
    {
        var oldControls = _content.Controls.Cast<Control>().ToArray();
        _content.Controls.Clear();
        _content.Visible = false;
        _scrollHost.Controls.Clear();
        _scrollHost.AutoScroll = false;
        _scrollHost.Padding = new Padding(14, 0, 14, 0);

        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Margin = Padding.Empty, Padding = Padding.Empty };
        _scrollHost.Controls.Add(host);

        var groups = new Dictionary<string, List<Control>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Home"] = [], ["Install"] = [], ["Switch Tools"] = [], ["Emulation"] = [],
            ["Game Center"] = [], ["Recovery"] = [], ["Diagnostics"] = [], ["Updates"] = []
        };
        foreach (var control in oldControls) groups[ClassifyAioControl(control)].Add(control);

        foreach (var group in groups)
        {
            var page = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                AutoScroll = true,
                Padding = new Padding(0, 0, 0, 10),
                Visible = false,
                Tag = group.Key
            };
            var heading = CreateAioPageHeading(group.Key);
            page.Controls.Add(heading);
            foreach (var control in group.Value)
            {
                control.Dock = DockStyle.Top;
                control.Margin = new Padding(0, 0, 0, 10);
                control.Visible = true;
                page.Controls.Add(control);
            }
            page.Controls.SetChildIndex(heading, 0);
            host.Controls.Add(page);
            _aioPages[group.Key] = page;
            EnableDarkScrollbars(page);
        }
        ShowAioPage("Home");
    }

    private static string ClassifyAioControl(Control control)
    {
        var text = CollectText(control);
        if (text.Contains("TARGET USB", StringComparison.OrdinalIgnoreCase) || text.Contains("INSTALLATION PROGRESS", StringComparison.OrdinalIgnoreCase) || text.Contains("USB WORKFLOW", StringComparison.OrdinalIgnoreCase)) return "Install";
        if (text.Contains("SWITCH TOOLS", StringComparison.OrdinalIgnoreCase) || text.Contains("SWITCH MANAGER", StringComparison.OrdinalIgnoreCase)) return "Switch Tools";
        if (text.Contains("EMULATION CENTER", StringComparison.OrdinalIgnoreCase)) return "Emulation";
        if (text.Contains("GAME CENTER", StringComparison.OrdinalIgnoreCase)) return "Game Center";
        if (text.Contains("RECOVERY CENTER", StringComparison.OrdinalIgnoreCase)) return "Recovery";
        if (text.Contains("UPDATE CENTER", StringComparison.OrdinalIgnoreCase)) return "Updates";
        if (text.Contains("LIVE OPERATION LOG", StringComparison.OrdinalIgnoreCase) || text.Contains("Safety gates active", StringComparison.OrdinalIgnoreCase)) return "Diagnostics";
        return "Home";
    }

    private static string CollectText(Control control)
    {
        var parts = new List<string>();
        void Walk(Control item)
        {
            if (!string.IsNullOrWhiteSpace(item.Text)) parts.Add(item.Text);
            foreach (Control child in item.Controls) Walk(child);
        }
        Walk(control);
        return string.Join("\n", parts);
    }

    private Control CreateAioPageHeading(string page)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = Theme.Background,
            Padding = new Padding(2, 0, 0, 8),
            Margin = new Padding(0, 0, 0, 6)
        };
        var title = new Label { Dock = DockStyle.Top, Height = 38, Text = page.ToUpperInvariant(), ForeColor = Theme.Text, Font = Theme.Mono(18, FontStyle.Bold), AutoEllipsis = true };
        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = page switch
            {
                "Home" => "Everything at a glance. Active operations stay visible above this area.",
                "Install" => "Prepare and write Switch Linux media with guarded, resumable steps.",
                "Switch Tools" => "Manage Switch components, AIO tools and RCM helpers.",
                "Emulation" => "Install and update the managed emulator stack and dependencies.",
                "Game Center" => "Index, verify and stage user-provided content through controlled installer workflows.",
                "Recovery" => "Create, inspect and restore MewNX checkpoints safely.",
                "Diagnostics" => "Logs, health signals and technical diagnostics.",
                "Updates" => "Check and install the latest MewNX build.",
                _ => ""
            },
            ForeColor = Theme.Muted,
            Font = Theme.UI(8.8f),
            AutoEllipsis = true
        };
        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private void ShowAioPage(string page)
    {
        if (!_aioPages.TryGetValue(page, out var target)) return;
        foreach (var item in _aioPages.Values) item.Visible = ReferenceEquals(item, target);
        var nav = _sidebar.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if (nav is not null)
        {
            foreach (var button in nav.Controls.OfType<Button>())
            {
                var active = string.Equals(button.Tag as string, page, StringComparison.OrdinalIgnoreCase);
                button.BackColor = active ? Theme.Surface2 : Color.Transparent;
                button.ForeColor = active ? Theme.Text : Theme.Muted;
            }
        }
        target.AutoScroll = true;
        target.VerticalScroll.Value = 0;
        target.HorizontalScroll.Value = 0;
        EnableDarkScrollbars(target);
        target.BringToFront();
    }

    private void RebrandExistingUi()
    {
        ReplaceTextRecursive(_sidebar, "MEW\nSWITCH", "MewNX");
        ReplaceTextRecursive(_sidebar, "MANAGER  //  0.3 ALPHA", "AIO TOOLKIT");
        ReplaceTextRecursive(_sidebar, "MANAGER  //  0.2 ALPHA", "AIO TOOLKIT");
        ReplaceTextRecursive(_compactNav, "MEW / SWITCH", "MewNX");
        ReplaceTextRecursive(_compactNav, "0.3 ALPHA", "AIO TOOLKIT");
        ReplaceTextRecursive(_compactNav, "0.2 ALPHA", "AIO TOOLKIT");
        ReplaceTextRecursive(_content, "SWITCH MANAGER", "SWITCH TOOLS");
        ReplaceTextRecursive(_content, "Switch Manager", "Switch Tools");
        ReplaceTextRecursive(_content, "MewSwitch", "MewNX");
        Text = "MewNX — Advanced Nintendo Switch Toolkit";
    }

    private static void EnableDarkScrollbars(Control control)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            if (!control.IsHandleCreated) control.HandleCreated += (_, _) => EnableDarkScrollbars(control);
            else SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
            foreach (Control child in control.Controls) EnableDarkScrollbars(child);
        }
        catch { }
    }

    [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);
}
