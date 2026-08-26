namespace MewSwitchManager.Models;

public enum SwitchToolKind
{
    Payload,
    Homebrew,
    Overlay
}

public sealed record SwitchToolDefinition(
    string Id,
    string Name,
    string Repository,
    SwitchToolKind Kind,
    string Destination,
    string AssetPattern,
    string Description,
    bool Optional = true)
{
    public override string ToString() => Name;
}

public sealed record SwitchToolStatus(
    SwitchToolDefinition Definition,
    bool Installed,
    string InstalledVersion,
    string AvailableVersion,
    bool UpdateAvailable,
    string? ReleaseUrl,
    string Message);
