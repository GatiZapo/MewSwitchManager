namespace MewSwitchManager.Models;

public sealed record UpdateInfo(
    bool UpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string? ReleaseUrl,
    string? ReleaseName,
    DateTimeOffset? PublishedAt,
    string? Error = null);
