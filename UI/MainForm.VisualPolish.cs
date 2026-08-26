namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    /// <summary>
    /// Final geometry pass. The legacy dashboard used fixed rows; MewNX now uses
    /// separate AIO pages, so the old row-height pass must not overwrite that layout.
    /// </summary>
    private void ApplyVisualPolish()
    {
        if (_aioPages.Count > 0)
        {
            _scrollHost.HorizontalScroll.Enabled = false;
            _scrollHost.HorizontalScroll.Visible = false;
            _scrollHost.Padding = new Padding(14, 0, 14, 0);
            _content.Visible = false;
            return;
        }

        _content.AutoSize = false;
        _content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _content.Dock = DockStyle.Top;
        _content.ColumnCount = 1;
        _content.RowCount = 7;
        _content.RowStyles.Clear();

        var heights = new[]
        {
            86,
            190,
            244,
            214,
            190,
            42,
            285
        };
        foreach (var height in heights) _content.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        _content.Height = heights.Sum();
        _content.Margin = Padding.Empty;
        _content.Padding = Padding.Empty;
        _content.BackColor = Theme.Background;

        foreach (Control child in _content.Controls) child.Dock = DockStyle.Fill;
        _scrollHost.HorizontalScroll.Enabled = false;
        _scrollHost.HorizontalScroll.Visible = false;
        _scrollHost.Resize -= ScrollHost_VisualPolishResize;
        _scrollHost.Resize += ScrollHost_VisualPolishResize;
        FitPolishedContentWidth();
    }

    private void ScrollHost_VisualPolishResize(object? sender, EventArgs e) => FitPolishedContentWidth();

    private void FitPolishedContentWidth()
    {
        if (_scrollHost.ClientSize.Width <= 0 || _aioPages.Count > 0) return;
        var width = _scrollHost.ClientSize.Width - _scrollHost.Padding.Horizontal - 2;
        if (_scrollHost.VerticalScroll.Visible) width -= SystemInformation.VerticalScrollBarWidth;
        _content.Width = Math.Max(640, width);
    }
}
