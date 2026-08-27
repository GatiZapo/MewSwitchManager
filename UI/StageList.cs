using MewNX.Models;

namespace MewNX.UI;

public sealed class StageList : Control
{
    public IReadOnlyList<StageRecord> Stages { get; set; } = [];

    public StageList()
    {
        Height = 214;
        DoubleBuffered = true;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Installation progress";
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
        using var headingFont = Theme.UI(12, FontStyle.Bold);
        using var subFont = Theme.UI(8, FontStyle.Regular);
        using var stageFont = Theme.Mono(7.4f, FontStyle.Bold);
        using var numberFont = Theme.Mono(7.1f, FontStyle.Bold);

        g.DrawString("INSTALLATION PROGRESS", headingFont, title, 18, 13);
        g.DrawString("Persistent state is saved automatically. Physical actions remain explicit.", subFont, muted, 18, 36);

        var stages = Enum.GetValues<InstallationStage>().Where(x => x != InstallationStage.Completed).ToArray();
        var top = 61;
        var rowHeight = Math.Max(22, (Height - top - 10) / Math.Max(1, stages.Length));
        var currentIndex = Array.FindIndex(stages, s => Stages.FirstOrDefault(x => x.Stage == s)?.State is StageState.Running or StageState.WaitingForUser or StageState.Failed);

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
                _ => Theme.Subtle
            };
            var y = top + i * rowHeight;
            var isCurrent = i == currentIndex || (currentIndex < 0 && state == StageState.Pending && i == stages.Length - 1);

            if (isCurrent)
            {
                using var currentBrush = new SolidBrush(Color.FromArgb(18, 21, 31));
                using var currentPen = new Pen(Color.FromArgb(49, 54, 71));
                var currentRect = new Rectangle(12, y - 3, Math.Max(40, Width - 24), rowHeight - 2);
                using var path = Theme.RoundedRect(currentRect, 8);
                g.FillPath(currentBrush, path);
                g.DrawPath(currentPen, path);
            }

            using var dot = new SolidBrush(color);
            g.FillEllipse(dot, 21, y + 8, 7, 7);
            g.DrawString($"{i + 1:00}", numberFont, muted, 38, y + 4);

            var name = StageName(stage);
            var available = Math.Max(90, Width - 190);
            while (name.Length > 5 && g.MeasureString(name, stageFont).Width > available) name = name[..^1];
            if (name.Length < StageName(stage).Length) name += "…";
            g.DrawString(name, stageFont, title, 70, y + 4);

            var stateText = state switch
            {
                StageState.Completed => "DONE",
                StageState.Running => "RUNNING",
                StageState.WaitingForUser => "WAITING",
                StageState.Failed => "FAILED",
                StageState.Warning => "WARNING",
                _ => "PENDING"
            };
            var stateWidth = g.MeasureString(stateText, stageFont).Width;
            using var stateBrush = new SolidBrush(color);
            g.DrawString(stateText, stageFont, stateBrush, Width - stateWidth - 20, y + 4);
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
