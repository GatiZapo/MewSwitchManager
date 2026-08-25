using System.Drawing.Drawing2D;

namespace MewSwitchManager.UI;

public static class Theme
{
    public static readonly Color Background = Color.FromArgb(6, 7, 12);
    public static readonly Color Surface = Color.FromArgb(13, 15, 23);
    public static readonly Color Surface2 = Color.FromArgb(17, 19, 28);
    public static readonly Color Border = Color.FromArgb(43, 46, 60);
    public static readonly Color Text = Color.FromArgb(242, 243, 249);
    public static readonly Color Muted = Color.FromArgb(137, 143, 163);
    public static readonly Color Pink = Color.FromArgb(255, 0, 212);
    public static readonly Color Blue = Color.FromArgb(0, 210, 255);
    public static readonly Color Green = Color.FromArgb(0, 255, 164);
    public static readonly Color Amber = Color.FromArgb(255, 190, 45);
    public static readonly Color Red = Color.FromArgb(255, 65, 105);
    public static readonly Color ProgressTrack = Color.FromArgb(30, 32, 43);

    public static Font UI(float size, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
    public static Font Mono(float size, FontStyle style = FontStyle.Regular) => new("Consolas", size, style);

    public static void Round(Graphics g, Rectangle rect, int radius, Color fill, Color border)
    {
        using var path = RoundedRect(rect, radius);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border, 1);
        g.FillPath(brush, path);
        g.DrawPath(pen, path);
    }

    public static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var r = Math.Max(1, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
        var d = r * 2;
        var p = new GraphicsPath();
        p.AddArc(rect.X, rect.Y, d, d, 180, 90);
        p.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        p.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        p.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
