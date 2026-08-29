namespace MewNX.Models;

public sealed class AppState
{
    public int SchemaVersion { get; set; } = 6;
    public InstallationStage CurrentStage { get; set; } = InstallationStage.EnvironmentPreflight;
    public bool LinuxDownloaded { get; set; }
    public bool LinuxVerified { get; set; }
    public string SelectedDiskNumber { get; set; } = "";
    public string SelectedDiskIdentity { get; set; } = "";
    public string SelectedDiskUniqueId { get; set; } = "";
    public string ProgressDiskNumber { get; set; } = "";
    public string ProgressDiskUniqueId { get; set; } = "";
    public List<StageRecord> Stages { get; set; } = [];
    public AutoPlan? AutoPlan { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void EnsureStages()
    {
        foreach (var stage in Enum.GetValues<InstallationStage>().Where(x => x != InstallationStage.Completed))
            if (Stages.All(x => x.Stage != stage)) Stages.Add(new StageRecord { Stage = stage });
    }

    public bool HasProgressForSelectedDisk()
    {
        if (string.IsNullOrWhiteSpace(ProgressDiskUniqueId) || string.IsNullOrWhiteSpace(SelectedDiskUniqueId))
            return false;

        return string.Equals(ProgressDiskUniqueId, SelectedDiskUniqueId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ProgressDiskNumber, SelectedDiskNumber, StringComparison.OrdinalIgnoreCase);
    }

    public void BindProgressToSelectedDisk()
    {
        ProgressDiskNumber = SelectedDiskNumber ?? "";
        ProgressDiskUniqueId = SelectedDiskUniqueId ?? "";
    }

    public void InvalidateDiskBoundProgressIfTargetChanged()
    {
        // Missing persisted identity is fail-safe: disk-bound progress must never be reused
        // when the checkpoint cannot positively identify its original target.
        if (HasProgressForSelectedDisk())
            return;

        foreach (var stage in Stages)
        {
            if (stage.Stage is InstallationStage.UsbStoragePreparation or InstallationStage.HekateSd or InstallationStage.SwitchConfiguration or InstallationStage.MewrootHandoff)
            {
                stage.State = StageState.Pending;
                stage.CompletedAt = null;
                stage.Message = "Pending: the selected storage target cannot be positively matched to the persisted checkpoint.";
            }
        }

        AutoPlan = null;
        CurrentStage = InstallationStage.EnvironmentPreflight;
        BindProgressToSelectedDisk();
    }

    public void ReconcilePersistedProgress()
    {
        EnsureStages();
        InvalidateDiskBoundProgressIfTargetChanged();

        if (LinuxVerified)
        {
            var image = Stages.First(x => x.Stage == InstallationStage.LinuxImage);
            image.State = StageState.Completed;
            image.Message = "Linux image is persisted as verified; the engine will revalidate it before destructive use.";
            image.CompletedAt ??= UpdatedAt;
            if (CurrentStage == InstallationStage.EnvironmentPreflight) CurrentStage = InstallationStage.UsbStoragePreparation;
        }
        else if (LinuxDownloaded)
        {
            var image = Stages.First(x => x.Stage == InstallationStage.LinuxImage);
            image.State = StageState.Warning;
            image.Message = "A previous session downloaded the image, but it is not currently marked verified.";
            if (CurrentStage == InstallationStage.EnvironmentPreflight) CurrentStage = InstallationStage.LinuxImage;
        }

        if (AutoPlan is not null)
        {
            AutoPlan.Steps ??= [];
            AutoPlan.TargetDiskNumber ??= "";
            AutoPlan.TargetDiskUniqueId ??= "";
            AutoPlan.CurrentStepId ??= "";
            if (!string.Equals(AutoPlan.TargetDiskUniqueId, SelectedDiskUniqueId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(AutoPlan.TargetDiskNumber, SelectedDiskNumber, StringComparison.OrdinalIgnoreCase))
            {
                AutoPlan = null;
            }
            else if (string.IsNullOrWhiteSpace(AutoPlan.CurrentStepId))
            {
                AutoPlan.CurrentStepId = AutoPlan.Steps.FirstOrDefault(x => x.State is AutoStepState.Pending or AutoStepState.Running or AutoStepState.WaitingForUser)?.Id ?? "";
            }
        }
    }
}
