using MewSwitchManager.Infrastructure;
using MewNX.Core;
using MewNX.Models;

namespace MewSwitchManager.Core;

public enum AutoRunOutcome { CompletedAutomaticSteps, WaitingForUser, Blocked, Failed }
public sealed record AutoRunResult(AutoRunOutcome Outcome, AutoPlan Plan, string Message);

public sealed class AutoModeService
{
    private readonly AutoModePlanner _planner = new();
    private readonly AppLogger _logger;
    public AutoModeService(AppLogger logger) => _logger = logger;

    public async Task<AutoRunResult> RunUntilUserGateAsync(InstallationEngine engine, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        var plan = _planner.BuildOrRefresh(engine.State); engine.SaveAutoPlan(plan);
        if (string.IsNullOrWhiteSpace(engine.State.SelectedDiskNumber)) return Block(plan, engine, "Select a safe USB target before starting Auto Mode.");
        try
        {
            var diagnostics = await new SystemDiagnostics(engine.Paths, engine.Config, _logger).RunAsync(engine, ct);
            var failure = diagnostics.Checks.FirstOrDefault(x => x.Severity == DiagnosticSeverity.Fail);
            if (failure is not null) return Block(plan, engine, $"Diagnostics blocked Auto Mode: {failure.Title}: {failure.Message}");

            var preflight = plan.Steps.First(x => x.Kind == AutoStepKind.EnvironmentPreflight);
            if (preflight.State != AutoStepState.Completed)
            {
                preflight.State = AutoStepState.Running;
                preflight.Message = diagnostics.HasWarnings ? "Diagnostics passed with warnings; running safety preflight." : "Running environment and target safety checks.";
                engine.SaveAutoPlan(plan);
                await engine.PreflightAsync(ct);
                Complete(preflight, diagnostics.HasWarnings ? "Preflight passed; warnings remain visible in Diagnostics." : "Preflight passed and target identity is safe.");
                engine.SaveAutoPlan(plan);
            }

            var image = plan.Steps.First(x => x.Kind == AutoStepKind.LinuxImage);
            if (image.State != AutoStepState.Completed)
            {
                image.State = AutoStepState.Running; image.Message = "Downloading/resuming and verifying the Linux image."; engine.SaveAutoPlan(plan);
                await engine.DownloadAndVerifyLinuxAsync(progress, ct);
                Complete(image, "Linux image downloaded and verified."); engine.SaveAutoPlan(plan);
            }

            plan = _planner.BuildOrRefresh(engine.State);
            var gate = plan.Steps.FirstOrDefault(x => x.State != AutoStepState.Completed);
            if (gate is null) { engine.SaveAutoPlan(plan); return new(AutoRunOutcome.CompletedAutomaticSteps, plan, "All available Auto Mode steps are complete."); }
            gate.State = AutoStepState.WaitingForUser; gate.Message = gate.RequiresConfirmation ? "Auto Mode paused at a user-controlled safety gate." : "Auto Mode is waiting for the next hardware/user checkpoint."; plan.CurrentStepId = gate.Id; engine.SaveAutoPlan(plan);
            return new(AutoRunOutcome.WaitingForUser, plan, $"Auto Mode paused at: {gate.Title}");
        }
        catch (OperationCanceledException) { plan = _planner.BuildOrRefresh(engine.State); engine.SaveAutoPlan(plan); throw; }
        catch (Exception ex)
        {
            var failed = plan.Steps.FirstOrDefault(x => x.State == AutoStepState.Running);
            if (failed is not null) { failed.State = AutoStepState.Failed; failed.Message = ex.Message; }
            plan.CurrentStepId = failed?.Id ?? plan.CurrentStepId; engine.SaveAutoPlan(plan); _logger.Error("Auto Mode failed", ex);
            return new(AutoRunOutcome.Failed, plan, ex.Message);
        }
    }

    private static AutoRunResult Block(AutoPlan plan, InstallationEngine engine, string message)
    {
        var step = plan.Steps.FirstOrDefault(x => x.State != AutoStepState.Completed);
        if (step is not null) { step.State = AutoStepState.Blocked; step.Message = message; plan.CurrentStepId = step.Id; }
        engine.SaveAutoPlan(plan); return new(AutoRunOutcome.Blocked, plan, message);
    }

    private static void Complete(AutoPlanStep step, string message) { step.State = AutoStepState.Completed; step.Message = message; step.CompletedAt = DateTimeOffset.UtcNow; }
}
