namespace MewSwitchManager.Models;

public enum SwitchComponent
{
    Hekate,
    Atmosphere,
    Dbi,
    Linux,
    Tools
}

public sealed record ComponentDefinition(
    SwitchComponent Id,
    string Name,
    string Repository,
    string DetectionPath,
    string Description,
    string ArchiveHint,
    bool PreserveExistingFiles = true)
{
    public override string ToString() => Name;
}

public sealed record ComponentStatus(
    ComponentDefinition Definition,
    bool Installed,
    string InstalledVersion,
    string AvailableVersion,
    string? ReleaseUrl,
    bool UpdateAvailable,
    string StatusMessage);

public sealed class ComponentManagerState
{
    public int SchemaVersion { get; set; } = 1;
    public string LastTargetRoot { get; set; } = "";
    public Dictionary<string, string> LastKnownVersions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
