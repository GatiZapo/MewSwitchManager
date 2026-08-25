namespace MewSwitchManager.UI;

public sealed class StatusCard : Control
{
    public string Heading { get; set; } = "STATUS";
    public string ValueText { get; set; } = "—";
    public Color Accent { get; set; } = Theme.Muted;

    public StatusCard()
    {
        Height = 88;
        MinimumSize = new Size(150, 76);
        DoubleBuffered = true;
        AccessibleRole = AccessibleRole.Grouping;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        Theme.Round(g, new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 12, Theme.Surface, Theme.Border);

        using var h = new SolidBrush(Theme.Muted);
        using var v = new SolidBrush(Theme.Text);
        using var dot = new SolidBrush(Accent);
        using var headingFont = Theme.Mono(7.5f, FontStyle.Bold);
        using var valueFont = Theme.UI(10.5f, FontStyle.Bold);

        g.DrawString(Heading.ToUpperInvariant(), headingFont, h, 15, 12);
        g.FillEllipse(dot, 16, 42, 7, 7);

        var available = Math.Max(60, Width - 44);
        var text = ValueText;
        while (text.Length > 4 && g.MeasureString(text, valueFont).Width > available)
            text = text[..^1];
        if (!string.Equals(text, ValueText, StringComparison.Ordinal)) text += "…";
        g.DrawString(text, valueFont, v, 30, 35);
    }
}
