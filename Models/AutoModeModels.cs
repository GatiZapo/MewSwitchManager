namespace MewNX.Models;

public enum AutoStepKind
{
    EnvironmentPreflight,
    LinuxImage,
    UsbWrite,
    HekateSd,
    SwitchConfiguration,
    MewrootHandoff
}

public enum AutoStepState
{
    Pending,
    Running,
    Completed,
    WaitingForUser,
    Blocked,
    Failed
}

public sealed class AutoPlanStep
{
    public string Id { get; set; } = "";
    public AutoStepKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool RequiresConfirmation { get; set; }
    public AutoStepState State { get; set; } = AutoStepState.Pending;
    public string Message { get; set; } = "";
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class AutoPlan
{
    public int SchemaVersion { get; set; } = 1;
    public string TargetDiskNumber { get; set; } = "";
    public string TargetDiskUniqueId { get; set; } = "";
    public List<AutoPlanStep> Steps { get; set; } = [];
    public string CurrentStepId { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AutoPlanStep? CurrentStep => Steps.FirstOrDefault(x => x.Id == CurrentStepId);
}
