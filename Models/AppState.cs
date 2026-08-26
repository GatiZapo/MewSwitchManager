namespace MewSwitchManager.Models;

public sealed class AppState
{
    public int SchemaVersion { get; set; } = 4;
    public InstallationStage CurrentStage { get; set; } = InstallationStage.EnvironmentPreflight;
    public bool LinuxDownloaded { get; set; }
    public bool LinuxVerified { get; set; }
    public string SelectedDiskNumber { get; set; } = "";
    public string SelectedDiskIdentity { get; set; } = "";
    public string SelectedDiskUniqueId { get; set; } = "";
    public List<StageRecord> Stages { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void EnsureStages()
    {
        foreach (var stage in Enum.GetValues<InstallationStage>().Where(x => x != InstallationStage.Completed))
            if (Stages.All(x => x.Stage != stage)) Stages.Add(new StageRecord { Stage = stage });
    }

    public void ReconcilePersistedProgress()
    {
        EnsureStages();
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
    }
}
