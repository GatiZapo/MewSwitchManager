namespace MewSwitchManager.Models;

public sealed class AppState
{
    public const int CurrentSchemaVersion = 5;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public InstallationStage CurrentStage { get; set; } = InstallationStage.EnvironmentPreflight;
    public bool LinuxDownloaded { get; set; }
    public bool LinuxVerified { get; set; }
    public long LinuxVerifiedSizeBytes { get; set; }
    public DateTimeOffset? LinuxVerifiedLastWriteUtc { get; set; }
    public string SelectedDiskNumber { get; set; } = "";
    public string SelectedDiskIdentity { get; set; } = "";
    public string SelectedDiskUniqueId { get; set; } = "";
    public List<StageRecord> Stages { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSuccessfulRunAt { get; set; }
    public string LastKnownAppVersion { get; set; } = "";

    public void EnsureStages()
    {
        Stages ??= [];
        foreach (var stage in Enum.GetValues<InstallationStage>().Where(x => x != InstallationStage.Completed))
            if (Stages.All(x => x.Stage != stage)) Stages.Add(new StageRecord { Stage = stage });

        Stages = Stages
            .Where(x => x.Stage != InstallationStage.Completed)
            .GroupBy(x => x.Stage)
            .Select(x => x.First())
            .OrderBy(x => x.Stage)
            .ToList();

        if (SchemaVersion < CurrentSchemaVersion)
            SchemaVersion = CurrentSchemaVersion;

        if (CurrentStage != InstallationStage.Completed)
        {
            var firstIncomplete = Stages.FirstOrDefault(x => x.State != StageState.Completed);
            if (firstIncomplete is not null)
                CurrentStage = firstIncomplete.Stage;
        }
    }

    public InstallationStage GetResumeStage()
    {
        EnsureStages();
        return Stages.FirstOrDefault(x => x.State != StageState.Completed)?.Stage ?? InstallationStage.Completed;
    }

    public bool IsStageComplete(InstallationStage stage)
        => Stages.FirstOrDefault(x => x.Stage == stage)?.State == StageState.Completed;
}
