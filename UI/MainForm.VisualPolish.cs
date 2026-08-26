namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    /// <summary>
    /// Stabilises the dashboard geometry after all sections have been added.
    /// The previous fixed row heights were smaller than the actual custom
    /// controls, which clipped the progress panel and made the dashboard look
    /// vertically broken.
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
            190,  // Target USB
            244,  // Health / progress
            214,  // Installation stages
            190,  // Operation log
            42,   // Footer
            285   // Update Center
        };

        foreach (var height in heights)
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

        _content.Height = heights.Sum();
        _content.Margin = Padding.Empty;
        _content.Padding = Padding.Empty;
        _content.BackColor = Theme.Background;

        foreach (Control child in _content.Controls)
            child.Dock = DockStyle.Fill;

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
        if (_scrollHost.ClientSize.Width <= 0) return;

        var width = _scrollHost.ClientSize.Width - _scrollHost.Padding.Horizontal - 2;
        if (_scrollHost.VerticalScroll.Visible) width -= SystemInformation.VerticalScrollBarWidth;
        _content.Width = Math.Max(640, width);
    }
}
