using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed class StageList : Control
{
    public IReadOnlyList<StageRecord> Stages { get; set; } = [];

    public StageList()
    {
        Height = 230;
        DoubleBuffered = true;
        AccessibleRole = AccessibleRole.List;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        Theme.Round(g, new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 14, Theme.Surface, Theme.Border);

        using var title = new SolidBrush(Theme.Text);
        using var muted = new SolidBrush(Theme.Muted);
        using var headingFont = Theme.UI(13, FontStyle.Bold);
        using var subFont = Theme.UI(8.5f);
        g.DrawString("AUTOMATED INSTALLATION", headingFont, title, 18, 15);
        g.DrawString("State is persisted automatically. Physical actions are explicit.", subFont, muted, 18, 40);

        var stages = Enum.GetValues<InstallationStage>().Where(x => x != InstallationStage.Completed).ToArray();
        var top = 72;
        var rowHeight = Math.Max(22, (Height - top - 14) / Math.Max(1, stages.Length));
        var font = Theme.Mono(7.5f, FontStyle.Bold);
        using (font)
        using (var textBrush = new SolidBrush(Theme.Text))
        using (var numBrush = new SolidBrush(Theme.Muted))
        {
            for (var i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                var record = Stages.FirstOrDefault(x => x.Stage == stage);
                var state = record?.State ?? StageState.Pending;
                var color = state switch
                {
                    StageState.Completed => Theme.Green,
                    StageState.Running => Theme.Pink,
                    StageState.Warning or StageState.WaitingForUser => Theme.Amber,
                    StageState.Failed => Theme.Red,
                    _ => Theme.Muted
                };
                var y = top + i * rowHeight;
                using var dot = new SolidBrush(color);
                g.FillEllipse(dot, 20, y + 6, 7, 7);
                g.DrawString($"{i + 1:00}", font, numBrush, 38, y + 2);

                var name = StageName(stage);
                var available = Math.Max(80, Width - 135);
                while (name.Length > 5 && g.MeasureString(name, font).Width > available) name = name[..^1];
                if (name.Length < StageName(stage).Length) name += "…";
                g.DrawString(name, font, textBrush, 70, y + 2);

                var stateText = state switch
                {
                    StageState.Completed => "DONE",
                    StageState.Running => "RUNNING",
                    StageState.WaitingForUser => "WAIT",
                    StageState.Failed => "FAILED",
                    _ => "PENDING"
                };
                var stateWidth = g.MeasureString(stateText, font).Width;
                using var stateBrush = new SolidBrush(color);
                g.DrawString(stateText, font, stateBrush, Width - stateWidth - 20, y + 2);
            }
        }
    }

    private static string StageName(InstallationStage stage) => stage switch
    {
        InstallationStage.EnvironmentPreflight => "ENVIRONMENT PREFLIGHT",
        InstallationStage.LinuxImage => "LINUX IMAGE",
        InstallationStage.UsbStoragePreparation => "USB / STORAGE PREPARATION",
        InstallationStage.HekateSd => "HEKATE / SD",
        InstallationStage.SwitchConfiguration => "SWITCH CONFIGURATION",
        InstallationStage.MewrootHandoff => "MEWROOT HANDOFF",
        _ => "COMPLETED"
    };
}
