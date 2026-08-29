using MewNX.Models;
using Xunit;

namespace MewNX.Tests;

public sealed class AppStateDiskProgressTests
{
    [Fact]
    public void SameDiskKeepsDiskBoundProgress()
    {
        var state = CreateState("\\\\.\\PHYSICALDRIVE7", "USB-A");
        state.Stages.Single(x => x.Stage == InstallationStage.UsbStoragePreparation).State = StageState.Completed;
        state.AutoPlan = new AutoPlan { TargetDiskNumber = state.SelectedDiskNumber, TargetDiskUniqueId = state.SelectedDiskUniqueId };
        state.BindProgressToSelectedDisk();

        state.ReconcilePersistedProgress();

        Assert.Equal(StageState.Completed, state.Stages.Single(x => x.Stage == InstallationStage.UsbStoragePreparation).State);
        Assert.NotNull(state.AutoPlan);
    }

    [Fact]
    public void DifferentDiskInvalidatesDiskBoundProgress()
    {
        var state = CreateState("\\\\.\\PHYSICALDRIVE7", "USB-A");
        state.Stages.Single(x => x.Stage == InstallationStage.UsbStoragePreparation).State = StageState.Completed;
        state.Stages.Single(x => x.Stage == InstallationStage.HekateSd).State = StageState.Completed;
        state.Stages.Single(x => x.Stage == InstallationStage.SwitchConfiguration).State = StageState.Completed;
        state.Stages.Single(x => x.Stage == InstallationStage.MewrootHandoff).State = StageState.Completed;
        state.AutoPlan = new AutoPlan { TargetDiskNumber = state.SelectedDiskNumber, TargetDiskUniqueId = state.SelectedDiskUniqueId };
        state.BindProgressToSelectedDisk();

        state.SelectedDiskNumber = "\\\\.\\PHYSICALDRIVE8";
        state.SelectedDiskUniqueId = "USB-B";
        state.ReconcilePersistedProgress();

        Assert.Equal(StageState.Pending, state.Stages.Single(x => x.Stage == InstallationStage.UsbStoragePreparation).State);
        Assert.Equal(StageState.Pending, state.Stages.Single(x => x.Stage == InstallationStage.HekateSd).State);
        Assert.Equal(StageState.Pending, state.Stages.Single(x => x.Stage == InstallationStage.SwitchConfiguration).State);
        Assert.Equal(StageState.Pending, state.Stages.Single(x => x.Stage == InstallationStage.MewrootHandoff).State);
        Assert.Null(state.AutoPlan);
        Assert.Equal(InstallationStage.EnvironmentPreflight, state.CurrentStage);
        Assert.Equal("USB-B", state.ProgressDiskUniqueId);
    }

    [Fact]
    public void UnboundDiskProgressIsNotReused()
    {
        var state = CreateState("\\\\.\\PHYSICALDRIVE7", "USB-A");
        state.Stages.Single(x => x.Stage == InstallationStage.UsbStoragePreparation).State = StageState.Completed;
        state.AutoPlan = new AutoPlan { TargetDiskNumber = state.SelectedDiskNumber, TargetDiskUniqueId = state.SelectedDiskUniqueId };

        state.ReconcilePersistedProgress();

        Assert.Equal(StageState.Pending, state.Stages.Single(x => x.Stage == InstallationStage.UsbStoragePreparation).State);
        Assert.Null(state.AutoPlan);
        Assert.Equal(InstallationStage.EnvironmentPreflight, state.CurrentStage);
        Assert.Equal("USB-A", state.ProgressDiskUniqueId);
    }

    private static AppState CreateState(string diskNumber, string uniqueId)
    {
        var state = new AppState
        {
            SelectedDiskNumber = diskNumber,
            SelectedDiskUniqueId = uniqueId,
            SelectedDiskIdentity = $"{diskNumber}:{uniqueId}"
        };
        state.EnsureStages();
        return state;
    }
}
