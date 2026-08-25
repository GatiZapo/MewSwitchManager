namespace MewSwitchManager.UI;

public sealed class StatusCard : Control
{
    public string Heading { get; set; } = "STATUS";
    public string ValueText { get; set; } = "—";
    public Color Accent { get; set; } = Theme.Muted;

    public StatusCard()
    {
        Height = 82;
        MinimumSize = new Size(150, 76);
        DoubleBuffered = true;
        AccessibleRole = AccessibleRole.Grouping;
        Margin = new Padding(0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        Theme.Round(g, rect, 12, Theme.Surface, Theme.Border);
        Theme.AccentLine(g, new Rectangle(14, 0, Math.Max(4, Width - 28), 0), Accent, 2);

        using var h = new SolidBrush(Theme.Muted);
        using var v = new SolidBrush(Theme.Text);
        using var dot = new SolidBrush(Accent);
        using var headingFont = Theme.Mono(7.2f, FontStyle.Bold);
        using var valueFont = Theme.UI(10.2f, FontStyle.Bold);

        g.DrawString(Heading.ToUpperInvariant(), headingFont, h, 15, 12);
        g.FillEllipse(dot, 16, 43, 7, 7);

        var available = Math.Max(70, Width - 42);
        var text = ValueText;
        while (text.Length > 4 && g.MeasureString(text, valueFont).Width > available)
            text = text[..^1];
        if (!string.Equals(text, ValueText, StringComparison.Ordinal)) text += "…";
        g.DrawString(text, valueFont, v, 30, 35);
    }
}
