namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    /// <summary>
    /// Stabilises the dashboard layout after all optional sections (including
    /// Update Center) have been added.  WinForms TableLayoutPanel can collapse
    /// Dock=Fill children when the parent is AutoSize=true and has no explicit
    /// row styles; this is what caused the header/section geometry to look
    /// broken on larger displays.
    /// </summary>
    private void ApplyVisualPolish()
    {
        _content.AutoSize = false;
        _content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _content.Dock = DockStyle.Top;
        _content.ColumnCount = 1;
        _content.RowCount = 7;
        _content.RowStyles.Clear();

        var heights = new[]
        {
            86,   // Header
            166,  // Target USB
            198,  // Health / progress
            214,  // Installation stages
            184,  // Operation log
            40,   // Footer
            270   // Update Center
        };

        foreach (var height in heights)
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

        _content.Height = heights.Sum();
        _content.Margin = Padding.Empty;
        _content.Padding = Padding.Empty;
        _content.BackColor = Theme.Background;

        foreach (Control child in _content.Controls)
        {
            child.Dock = DockStyle.Fill;
        }

        // Keep the content exactly inside the available viewport. This avoids
        // the subtle horizontal clipping caused by AutoScroll + AutoSize.
        _scrollHost.HorizontalScroll.Enabled = false;
        _scrollHost.HorizontalScroll.Visible = false;
        _scrollHost.Resize -= ScrollHost_VisualPolishResize;
        _scrollHost.Resize += ScrollHost_VisualPolishResize;
        FitPolishedContentWidth();
    }

    private void ScrollHost_VisualPolishResize(object? sender, EventArgs e)
        => FitPolishedContentWidth();

    private void FitPolishedContentWidth()
    {
        if (_scrollHost.ClientSize.Width <= 0)
            return;

        var width = _scrollHost.ClientSize.Width
            - _scrollHost.Padding.Horizontal
            - 2;

        if (_scrollHost.VerticalScroll.Visible)
            width -= SystemInformation.VerticalScrollBarWidth;

        _content.Width = Math.Max(640, width);
    }
}
