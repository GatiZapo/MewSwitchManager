namespace MewNX.Models;

public enum InstallationStage
{
    EnvironmentPreflight = 1,
    LinuxImage = 2,
    UsbStoragePreparation = 3,
    HekateSd = 4,
    SwitchConfiguration = 5,
    MewrootHandoff = 6,
    Completed = 7
}

public enum StageState
{
    Pending,
    Running,
    Completed,
    WaitingForUser,
    Warning,
    Failed
}

public sealed class StageRecord
{
    public InstallationStage Stage { get; set; }
    public StageState State { get; set; } = StageState.Pending;
    public string Message { get; set; } = "";
    public DateTimeOffset? CompletedAt { get; set; }
}
