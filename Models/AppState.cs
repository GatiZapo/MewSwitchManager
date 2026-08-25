namespace MewSwitchManager.Models;

public sealed class AppState
{
    public int SchemaVersion { get; set; } = 3;
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
}
