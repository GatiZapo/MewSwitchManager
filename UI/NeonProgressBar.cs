namespace MewSwitchManager.UI;

public sealed class NeonProgressBar : Control
{
    private double _value;
    public double Value { get => _value; set { _value = Math.Clamp(value, 0, 100); Invalidate(); } }
    public string Caption { get; set; } = "READY";
    public string Detail { get; set; } = "";
    public string RightText { get; set; } = "";

    public NeonProgressBar()
    {
        Height = 126;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        AccessibleRole = AccessibleRole.ProgressBar;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Theme.Round(g, new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 12, Theme.Surface, Theme.Border);
        using var muted = new SolidBrush(Theme.Muted);
        using var title = new SolidBrush(Theme.Text);
        using var accent = new SolidBrush(Theme.Blue);
        using var white = new SolidBrush(Theme.Text);
        using var labelFont = Theme.Mono(8, FontStyle.Bold);
        using var titleFont = Theme.UI(18, FontStyle.Bold);
        using var detailFont = Theme.UI(8.5f);
        using var monoFont = Theme.Mono(8, FontStyle.Bold);

        g.DrawString("INSTALLATION HEALTH", labelFont, muted, 20, 12);
        g.DrawString(Caption, titleFont, title, 20, 31);

        var right = string.IsNullOrWhiteSpace(RightText) ? "" : RightText;
        var rightWidth = string.IsNullOrEmpty(right) ? 0 : g.MeasureString(right, monoFont).Width;
        var barX = Math.Clamp(Width / 3, 210, 320);
        var barWidth = Math.Max(100, Width - barX - (int)rightWidth - 48);
        var bar = new Rectangle(barX, 42, barWidth, 9);
        using (var background = new SolidBrush(Theme.ProgressTrack)) g.FillRectangle(background, bar);
        var fillWidth = (int)Math.Round(bar.Width * (_value / 100d));
        if (fillWidth > 0)
        {
            using var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(bar.X, bar.Y, fillWidth, bar.Height), Theme.Pink, Theme.Blue, 0f);
            g.FillRectangle(gradient, new Rectangle(bar.X, bar.Y, fillWidth, bar.Height));
        }

        if (!string.IsNullOrWhiteSpace(right)) g.DrawString(right, monoFont, accent, Width - rightWidth - 20, 38);
        g.DrawString($"{_value:0}%", monoFont, white, Width - 58, 68);

        var maxWidth = Math.Max(140, Width - 100);
        var detail = Detail ?? "";
        while (detail.Length > 4 && g.MeasureString(detail, detailFont).Width > maxWidth) detail = detail[..^1];
        if (detail.Length < Detail.Length) detail += "…";
        g.DrawString(detail, detailFont, muted, 20, 70);
    }
}
