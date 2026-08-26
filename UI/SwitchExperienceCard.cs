using MewSwitchManager.Core;
using MewSwitchManager.Infrastructure;

namespace MewSwitchManager.UI;

public sealed class SwitchExperienceCard : Panel
{
    private readonly Label _summary = new();
    private readonly Label _details = new();
    private readonly Button _recommended = new();
    private readonly Button _checkpoint = new();
    private readonly Button _rescan = new();

    public event EventHandler? RecommendedClicked;
    public event EventHandler? CheckpointClicked;
    public event EventHandler? RescanClicked;

    public SwitchExperienceCard()
    {
        BackColor = Theme.Surface;
        Padding = new Padding(16);
        Height = 230;
        Dock = DockStyle.Top;

        var title = new Label { Text = "SWITCH HEALTH", Dock = DockStyle.Top, Height = 30, ForeColor = Theme.Text, Font = Theme.UI(13, FontStyle.Bold) };
        _summary.Dock = DockStyle.Top; _summary.Height = 38; _summary.ForeColor = Theme.Muted; _summary.Font = Theme.UI(9);
        _details.Dock = DockStyle.Fill; _details.ForeColor = Theme.Muted; _details.Font = Theme.Mono(7.5f);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, WrapContents = false, BackColor = Theme.Surface };
        Style(_recommended, "PREPARE RECOMMENDED", Theme.Pink, 170);
        Style(_checkpoint, "CREATE CHECKPOINT", Theme.Blue, 155);
        Style(_rescan, "RESCAN", Theme.Surface2, 90);
        _recommended.Click += (_, _) => RecommendedClicked?.Invoke(this, EventArgs.Empty);
        _checkpoint.Click += (_, _) => CheckpointClicked?.Invoke(this, EventArgs.Empty);
        _rescan.Click += (_, _) => RescanClicked?.Invoke(this, EventArgs.Empty);
        actions.Controls.Add(_recommended); actions.Controls.Add(_checkpoint); actions.Controls.Add(_rescan);
        Controls.Add(_details); Controls.Add(actions); Controls.Add(_summary); Controls.Add(title);
    }

    public void ShowLoading() { _summary.Text = "Scanning Switch storage…"; _details.Text = "Checking Hekate • Atmosphère • emuMMC • tools • configuration"; }
    public void Show(SwitchExperienceSummary result)
    {
        _summary.Text = result.Summary;
        var lines = new List<string>();
        lines.AddRange(result.Healthy.Select(x => "✓ " + x));
        lines.AddRange(result.Warnings.Select(x => "! " + x));
        if (result.Recommendations.Count > 0) lines.Add("Recommended: " + string.Join(", ", result.Recommendations.Where(x => x.Recommended).Select(x => x.ToolId)));
        _details.Text = string.Join(Environment.NewLine, lines);
        _recommended.Enabled = result.Recommendations.Any(x => x.Recommended);
    }

    private static void Style(Button b, string text, Color back, int width)
    {
        b.Text = text; b.Width = width; b.Height = 32; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = back; b.ForeColor = Theme.Text; b.Font = Theme.UI(7.5f, FontStyle.Bold);
    }
}
