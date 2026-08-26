namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    /// <summary>
    /// Keeps the custom-drawn sections from being clipped when their internal
    /// controls grow. WinForms Dock/AutoSize can otherwise retain the old fixed
    /// height from the initial layout pass.
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        BeginInvoke(ApplyLayoutFixes);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (IsHandleCreated && !IsDisposed)
            BeginInvoke(ApplyLayoutFixes);
    }

    private void ApplyLayoutFixes()
    {
        if (IsDisposed || _content.Controls.Count < 6) return;

        // Header / target / health / stages / log / footer.
        var health = _content.Controls[2];
        var stages = _content.Controls[3];
        var log = _content.Controls[4];
        var footer = _content.Controls[5];

        // Four status cards (88) + breathing room (10) + progress panel (138).
        // The previous fixed 222px host clipped the progress panel at the bottom.
        health.Height = 244;
        _progress.Height = 138;
        stages.Height = Math.Max(214, _stages.Height);
        log.Height = Math.Max(190, log.Height);
        footer.Height = 42;

        if (_progress.Parent is Panel healthHost && healthHost.Controls.Count > 0 && healthHost.Controls[0] is TableLayoutPanel cards)
        {
            cards.Height = 88;
            for (var i = 0; i < cards.Controls.Count; i++)
                cards.Controls[i].Height = 88;
        }

        // At compact widths, let the four health cards become a clean 2x2 grid
        // instead of squeezing four long labels into narrow columns.
        if (_progress.Parent is Panel compactHost && compactHost.Controls.Count > 0 && compactHost.Controls[0] is TableLayoutPanel compactCards)
        {
            var compact = ClientSize.Width < 980;
            if (compact)
            {
                compactCards.RowCount = 2;
                compactCards.ColumnCount = 2;
                compactCards.Height = 176;
                compactCards.RowStyles.Clear();
                compactCards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                compactCards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                compactCards.ColumnStyles.Clear();
                compactCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                compactCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

                var cards = compactCards.Controls.Cast<Control>().ToArray();
                for (var i = 0; i < cards.Length; i++)
                {
                    var card = cards[i];
                    compactCards.SetColumn(card, i % 2);
                    compactCards.SetRow(card, i / 2);
                    card.Margin = new Padding(2);
                }
                health.Height = 332;
                _progress.Margin = new Padding(0, 10, 0, 0);
            }
            else
            {
                compactCards.RowCount = 1;
                compactCards.ColumnCount = 4;
                compactCards.Height = 88;
                compactCards.RowStyles.Clear();
                compactCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                compactCards.ColumnStyles.Clear();
                for (var i = 0; i < 4; i++) compactCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                var cards = compactCards.Controls.Cast<Control>().ToArray();
                for (var i = 0; i < cards.Length; i++)
                {
                    compactCards.SetColumn(cards[i], i);
                    compactCards.SetRow(cards[i], 0);
                    cards[i].Margin = new Padding(i == 0 ? 0 : 4, 0, i == cards.Length - 1 ? 0 : 4, 0);
                }
                health.Height = 244;
            }
        }

        FitContentWidth();
        _content.PerformLayout();
        _scrollHost.PerformLayout();
    }
}
