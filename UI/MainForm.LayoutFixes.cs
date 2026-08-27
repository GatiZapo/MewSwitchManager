namespace MewNX.UI;

public sealed partial class MainForm
{
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        BeginInvoke(ApplyLayoutFixes);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (IsHandleCreated && !IsDisposed) BeginInvoke(ApplyLayoutFixes);
    }

    private void ApplyLayoutFixes()
    {
        if (IsDisposed || _content.Controls.Count < 6) return;

        var health = _content.Controls[2];
        var stages = _content.Controls[3];
        var log = _content.Controls[4];
        var footer = _content.Controls[5];
        var compact = ClientSize.Width < 980;

        health.Height = compact ? 332 : 244;
        _progress.Height = 138;
        stages.Height = Math.Max(214, _stages.Height);
        log.Height = Math.Max(190, log.Height);
        footer.Height = 42;

        if (_content.RowStyles.Count > 2)
            _content.RowStyles[2].Height = health.Height;

        if (_progress.Parent is Panel healthHost && healthHost.Controls.Count > 0 && healthHost.Controls[0] is TableLayoutPanel cards)
        {
            if (compact)
            {
                cards.RowCount = 2;
                cards.ColumnCount = 2;
                cards.Height = 176;
                cards.RowStyles.Clear();
                cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                cards.ColumnStyles.Clear();
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                var controls = cards.Controls.Cast<Control>().ToArray();
                for (var i = 0; i < controls.Length; i++)
                {
                    cards.SetColumn(controls[i], i % 2);
                    cards.SetRow(controls[i], i / 2);
                    controls[i].Margin = new Padding(2);
                }
            }
            else
            {
                cards.RowCount = 1;
                cards.ColumnCount = 4;
                cards.Height = 88;
                cards.RowStyles.Clear();
                cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                cards.ColumnStyles.Clear();
                for (var i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                var controls = cards.Controls.Cast<Control>().ToArray();
                for (var i = 0; i < controls.Length; i++)
                {
                    cards.SetColumn(controls[i], i);
                    cards.SetRow(controls[i], 0);
                    controls[i].Margin = new Padding(i == 0 ? 0 : 4, 0, i == controls.Length - 1 ? 0 : 4, 0);
                }
            }
        }

        FitContentWidth();
        _content.PerformLayout();
        _scrollHost.PerformLayout();
    }
}
