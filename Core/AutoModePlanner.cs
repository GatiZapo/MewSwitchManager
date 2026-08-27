using MewNX.Models;

namespace MewNX.Core;

public sealed class AutoModePlanner
{
    public AutoPlan BuildOrRefresh(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var plan = state.AutoPlan ?? new AutoPlan();
        var targetChanged = !string.Equals(plan.TargetDiskNumber, state.SelectedDiskNumber, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(plan.TargetDiskUniqueId, state.SelectedDiskUniqueId, StringComparison.OrdinalIgnoreCase);
        if (targetChanged || plan.Steps.Count != 6) plan = CreatePlan(state);
        else ReconcileStepStates(plan, state);
        plan.TargetDiskNumber = state.SelectedDiskNumber;
        plan.TargetDiskUniqueId = state.SelectedDiskUniqueId;
        plan.CurrentStepId = plan.Steps.FirstOrDefault(x => x.State != AutoStepState.Completed)?.Id ?? "";
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        return plan;
    }

    private static AutoPlan CreatePlan(AppState state)
    {
        var plan = new AutoPlan
        {
            TargetDiskNumber = state.SelectedDiskNumber,
            TargetDiskUniqueId = state.SelectedDiskUniqueId,
            Steps =
            [
                new() { Id = "preflight", Kind = AutoStepKind.EnvironmentPreflight, Title = "Environment preflight", Description = "Validate Windows, dependencies, WSL and the selected USB target." },
                new() { Id = "linux-image", Kind = AutoStepKind.LinuxImage, Title = "Download + verify Linux", Description = "Resume the cached download when possible and verify the final image before use." },
                new() { Id = "usb-write", Kind = AutoStepKind.UsbWrite, Title = "Prepare Linux USB", Description = "Revalidate the physical target and write the verified Linux image. This is destructive.", RequiresConfirmation = true },
                new() { Id = "hekate-sd", Kind = AutoStepKind.HekateSd, Title = "Hekate / SD handoff", Description = "Continue the SD-side preparation after the physical USB write is complete.", RequiresConfirmation = true },
                new() { Id = "switch-config", Kind = AutoStepKind.SwitchConfiguration, Title = "Switch configuration", Description = "Apply the remaining Switch-side configuration only after the hardware checkpoint is satisfied.", RequiresConfirmation = true },
                new() { Id = "mewroot", Kind = AutoStepKind.MewrootHandoff, Title = "Mewroot handoff", Description = "Final handoff and user-controlled boot into the prepared environment.", RequiresConfirmation = true }
            ]
        };
        ReconcileStepStates(plan, state);
        return plan;
    }

    private static void ReconcileStepStates(AutoPlan plan, AppState state)
    {
        Set(plan, "preflight", state.Stages.Any(x => x.Stage == InstallationStage.EnvironmentPreflight && x.State == StageState.Completed));
        Set(plan, "linux-image", state.LinuxVerified || state.Stages.Any(x => x.Stage == InstallationStage.LinuxImage && x.State == StageState.Completed));
        Set(plan, "usb-write", state.Stages.Any(x => x.Stage == InstallationStage.UsbStoragePreparation && x.State == StageState.Completed));
        Set(plan, "hekate-sd", state.Stages.Any(x => x.Stage == InstallationStage.HekateSd && x.State == StageState.Completed));
        Set(plan, "switch-config", state.Stages.Any(x => x.Stage == InstallationStage.SwitchConfiguration && x.State == StageState.Completed));
        Set(plan, "mewroot", state.Stages.Any(x => x.Stage == InstallationStage.MewrootHandoff && x.State == StageState.Completed));
        var firstIncomplete = plan.Steps.FirstOrDefault(x => x.State != AutoStepState.Completed);
        if (firstIncomplete is not null && firstIncomplete.RequiresConfirmation && firstIncomplete.State == AutoStepState.Pending)
        {
            firstIncomplete.State = AutoStepState.WaitingForUser;
            firstIncomplete.Message = "Waiting for explicit user confirmation before continuing.";
        }
    }

    private static void Set(AutoPlan plan, string id, bool completed)
    {
        var step = plan.Steps.FirstOrDefault(x => x.Id == id);
        if (step is null) return;
        if (completed)
        {
            step.State = AutoStepState.Completed;
            step.Message = "Completed and reconciled from persisted installation state.";
            step.CompletedAt ??= DateTimeOffset.UtcNow;
        }
        else if (step.State == AutoStepState.Completed)
        {
            step.State = AutoStepState.Pending;
            step.CompletedAt = null;
            step.Message = "Pending.";
        }
    }
}
