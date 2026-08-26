using MewSwitchManager.Core;
using MewSwitchManager.Models;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private AutoModeService _autoModeService = null!;
    private readonly Button _autoRun = new();
    private readonly Button _autoRefresh = new();
    private readonly Button _autoContinue = new();
    private readonly Label _autoSummary = new();
    private readonly Label _autoPlanText = new();

    private void InitializeAutoMode()
    {
        _autoModeService = new AutoModeService(_logger);
        _content.RowCount = Math.Max(_content.RowCount, 12);
        _content.Controls.Add(BuildAutoModeSection(), 0, 11);
        var nav = _sidebar.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
        if (nav is not null)
        {
            var button = NavigationButton("06   AUTO MODE", false, 11);
            button.Width = 194;
            nav.Controls.Add(button);
        }
        foreach (var label in Flatten(_sidebar).OfType<Label>())
        {
            if (label.Text.Contains("0.3 ALPHA", StringComparison.OrdinalIgnoreCase)) label.Text = "MANAGER  //  0.4 ALPHA";
        }
        foreach (var label in Flatten(_compactNav).OfType<Label>())
        {
            if (label.Text.Contains("0.3 ALPHA", StringComparison.OrdinalIgnoreCase)) label.Text = "0.4 ALPHA";
        }
        RefreshAutoPlan();
    }

    private Control BuildAutoModeSection()
    {
        var card = CreateCard();
        card.Height = 260;
        card.Padding = new Padding(16);
        var title = new Label { Dock = DockStyle.Top, Height = 29, Text = "AUTO MODE / OPERATION PLAN", ForeColor = Theme.Text, Font = Theme.UI(12.5f, FontStyle.Bold) };
        var subtitle = new Label { Dock = DockStyle.Top, Height = 35, Text = "Runs safe automatic steps, persists checkpoints and stops before destructive or hardware-dependent actions.", ForeColor = Theme.Muted, Font = Theme.UI(8.2f) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, BackColor = Theme.Surface, Margin = Padding.Empty };
        StyleButton(_autoRun, "RUN AUTO MODE", Theme.Pink, 150);
        StyleButton(_autoRefresh, "REBUILD PLAN", Theme.Blue, 132);
        StyleButton(_autoContinue, "CONTINUE SAFE GATE", Theme.Red, 160);
        _autoRun.Margin = new Padding(0, 0, 8, 0);
        _autoRefresh.Margin = new Padding(0, 0, 8, 0);
        _autoRun.Click += async (_, _) => await RunAutoModeAsync();
        _autoRefresh.Click += (_, _) => RefreshAutoPlan();
        _autoContinue.Click += async (_, _) => await ContinueAutoGateAsync();
        actions.Controls.Add(_autoRun);
        actions.Controls.Add(_autoRefresh);
        actions.Controls.Add(_autoContinue);
        _autoSummary.Dock = DockStyle.Top;
        _autoSummary.Height = 28;
        _autoSummary.ForeColor = Theme.Muted;
        _autoSummary.Font = Theme.Mono(7.8f, FontStyle.Bold);
        _autoPlanText.Dock = DockStyle.Fill;
        _autoPlanText.ForeColor = Theme.Muted;
        _autoPlanText.Font = Theme.Mono(7.4f);
        _autoPlanText.Padding = new Padding(0, 6, 0, 0);
        card.Controls.Add(_autoPlanText);
        card.Controls.Add(_autoSummary);
        card.Controls.Add(actions);
        card.Controls.Add(subtitle);
        card.Controls.Add(title);
        return card;
    }

    private void RefreshAutoPlan()
    {
        var plan = new AutoModePlanner().BuildOrRefresh(_engine.State);
        _engine.SaveAutoPlan(plan);
        RenderAutoPlan(plan);
    }

    private async Task RunAutoModeAsync()
    {
        if (_operationCts is not null) return;
        _operationCts = new CancellationTokenSource();
        try
        {
            _autoRun.Enabled = false;
            _autoRefresh.Enabled = false;
            SetStatus("●  AUTO MODE", Theme.Pink);
            var result = await _autoModeService.RunUntilUserGateAsync(_engine, new Progress<DownloadProgress>(RenderDownloadProgress), _operationCts.Token);
            RenderAutoPlan(result.Plan);
            if (result.Outcome == AutoRunOutcome.WaitingForUser)
            {
                SetStatus("●  AUTO MODE / USER CHECKPOINT", Theme.Amber);
                MessageBox.Show(this, result.Message + "\n\nNo destructive action was performed automatically.", "Auto Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (result.Outcome == AutoRunOutcome.CompletedAutomaticSteps) SetStatus("●  AUTO MODE COMPLETE", Theme.Green);
            else if (result.Outcome == AutoRunOutcome.Blocked) { SetStatus("●  AUTO MODE BLOCKED", Theme.Red); MessageBox.Show(this, result.Message, "Auto Mode blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            else { SetStatus("●  AUTO MODE FAILED", Theme.Red); MessageBox.Show(this, result.Message, "Auto Mode failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            UpdateUi();
        }
        catch (OperationCanceledException)
        {
            SetStatus("●  AUTO MODE PAUSED", Theme.Amber);
            RefreshAutoPlan();
        }
        catch (Exception ex)
        {
            _logger.Error("Auto Mode UI operation failed", ex);
            SetStatus("●  AUTO MODE FAILED", Theme.Red);
            MessageBox.Show(this, ex.Message, "Auto Mode", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            _autoRun.Enabled = true;
            _autoRefresh.Enabled = true;
            UpdateAutoButtons();
            UpdateActionButtons();
        }
    }

    private async Task ContinueAutoGateAsync()
    {
        if (_engine.State.AutoPlan?.CurrentStep?.Kind != AutoStepKind.UsbWrite)
        {
            MessageBox.Show(this, "No hay una puerta destructiva de USB pendiente. Ejecuta AUTO MODE para reconstruir el plan.", "Auto Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        await StartInstallationAsync();
        RefreshAutoPlan();
    }

    private void RenderAutoPlan(AutoPlan plan)
    {
        var completed = plan.Steps.Count(x => x.State == AutoStepState.Completed);
        var current = plan.CurrentStep;
        _autoSummary.Text = $"PROGRESS {completed}/{plan.Steps.Count}    TARGET {(!string.IsNullOrWhiteSpace(plan.TargetDiskNumber) ? "DISK " + plan.TargetDiskNumber : "NOT SELECTED")}    NEXT {(current?.Title ?? "NONE").ToUpperInvariant()}";
        _autoSummary.ForeColor = current?.State == AutoStepState.WaitingForUser ? Theme.Amber : completed == plan.Steps.Count ? Theme.Green : Theme.Blue;
        _autoPlanText.Text = string.Join(Environment.NewLine, plan.Steps.Select((step, index) => $"{StepGlyph(step.State)} {index + 1:00}  {step.Title,-25} {step.State.ToString().ToUpperInvariant(),-16} {step.Message}"));
        UpdateAutoButtons();
    }

    private void UpdateAutoButtons()
    {
        var current = _engine.State.AutoPlan?.CurrentStep;
        var busy = _operationCts is not null;
        _autoContinue.Enabled = !busy && current?.Kind == AutoStepKind.UsbWrite && current.State == AutoStepState.WaitingForUser && _engine.IsSelectedDiskSafe();
    }

    private static string StepGlyph(AutoStepState state) => state switch
    {
        AutoStepState.Completed => "✓",
        AutoStepState.Running => "▶",
        AutoStepState.WaitingForUser => "!",
        AutoStepState.Blocked => "×",
        AutoStepState.Failed => "✗",
        _ => "·"
    };
}
