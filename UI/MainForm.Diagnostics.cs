using MewSwitchManager.Core;

namespace MewSwitchManager.UI;

public sealed partial class MainForm
{
    private readonly Button _diagnosticsButton = new();
    private readonly Button _repairButton = new();
    private readonly Label _diagnosticsStatus = new();

    private void InitializeDiagnostics()
    {
        _content.RowCount = Math.Max(_content.RowCount, 12);
        _content.Controls.Add(BuildDiagnosticsSection(), 0, 10);
    }

    private Control BuildDiagnosticsSection()
    {
        var card = CreateCard();
        card.Height = 350;
        card.Padding = new Padding(16);
        var title = new Label { Dock = DockStyle.Top, Height = 28, Text = "SYSTEM HEALTH / REPAIR", ForeColor = Theme.Text, Font = Theme.UI(12, FontStyle.Bold) };
        _diagnosticsStatus.Dock = DockStyle.Fill;
        _diagnosticsStatus.ForeColor = Theme.Muted;
        _diagnosticsStatus.Font = Theme.Mono(7.3f);
        _diagnosticsStatus.Text = "No diagnostic scan run yet.";
        _diagnosticsStatus.AutoEllipsis = false;

        StyleButton(_diagnosticsButton, "RUN FULL DIAGNOSTICS", Theme.Blue, 205);
        _diagnosticsButton.Click += async (_, _) => await RunDiagnosticsAsync();

        StyleButton(_repairButton, "SAFE REPAIR", Theme.Pink, 150);
        _repairButton.Click += async (_, _) => await RunRepairAsync();

        var row = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, WrapContents = true, BackColor = Theme.Surface, Padding = new Padding(0, 4, 0, 0) };
        row.Controls.Add(_diagnosticsButton);
        row.Controls.Add(_repairButton);
        card.Controls.Add(_diagnosticsStatus);
        card.Controls.Add(row);
        card.Controls.Add(title);
        return card;
    }

    private async Task RunDiagnosticsAsync()
    {
        _diagnosticsButton.Enabled = false;
        try
        {
            var report = await new SystemDiagnostics(_paths, _config, _logger).RunAsync(_engine);
            RenderDiagnostics(report);
        }
        catch (Exception ex)
        {
            _logger.Error("Diagnostics failed", ex);
            _diagnosticsStatus.Text = "Diagnostic scan failed: " + ex.Message;
            _diagnosticsStatus.ForeColor = Theme.Red;
            SetStatus("●  DIAGNOSTICS FAILED", Theme.Red);
        }
        finally { _diagnosticsButton.Enabled = true; }
    }

    private async Task RunRepairAsync()
    {
        _repairButton.Enabled = false;
        try
        {
            var report = await new RepairService(_paths, _config, _logger).RepairAsync();
            var changed = report.Actions.Count(x => x.Changed);
            _diagnosticsStatus.Text = $"SAFE REPAIR • {changed} change(s)\n\n" +
                string.Join("\n", report.Actions.Select(x => $"{(x.Changed ? "•" : "·")} {x.Id}: {x.Message}"));
            _diagnosticsStatus.ForeColor = changed > 0 ? Theme.Pink : Theme.Green;
            SetStatus(changed > 0 ? "●  SAFE REPAIR COMPLETE — RE-SCANNING" : "●  NOTHING REQUIRED — RE-SCANNING", changed > 0 ? Theme.Pink : Theme.Green);
            await RunDiagnosticsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Safe repair failed", ex);
            _diagnosticsStatus.Text = "Safe repair failed: " + ex.Message;
            _diagnosticsStatus.ForeColor = Theme.Red;
            SetStatus("●  REPAIR FAILED", Theme.Red);
        }
        finally { _repairButton.Enabled = true; }
    }

    private void RenderDiagnostics(DiagnosticReport report)
    {
        var pass = report.Checks.Count(x => x.Severity == DiagnosticSeverity.Pass);
        var warnings = report.Checks.Count(x => x.Severity == DiagnosticSeverity.Warning);
        var failures = report.Checks.Count(x => x.Severity == DiagnosticSeverity.Fail);
        _diagnosticsStatus.Text = $"PASS {pass}   WARN {warnings}   FAIL {failures}\n\n" +
            string.Join("\n", report.Checks.Select(x => $"{SeverityGlyph(x.Severity)} {x.Title}: {x.Message}"));
        _diagnosticsStatus.ForeColor = failures > 0 ? Theme.Red : warnings > 0 ? Theme.Amber : Theme.Green;
        SetStatus(failures > 0 ? "●  DIAGNOSTICS FOUND PROBLEMS" : warnings > 0 ? "●  DIAGNOSTICS: WARNINGS" : "●  DIAGNOSTICS OK", failures > 0 ? Theme.Red : warnings > 0 ? Theme.Amber : Theme.Green);
    }

    private static string SeverityGlyph(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Pass => "✓",
        DiagnosticSeverity.Warning => "!",
        _ => "✗"
    };
}
