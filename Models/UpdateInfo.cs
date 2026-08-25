namespace MewSwitchManager.Models;

public sealed record UpdateInfo(
    bool IsAvailable,
    string CurrentVersion,
    string LatestVersion,
    string TagName,
    string ReleaseUrl,
    string ReleaseName,
    string ReleaseNotes,
    string? AssetUrl,
    string? AssetName)
{
    public static UpdateInfo NoUpdate(string current, string latest = "") =>
        new(false, current, latest, "", "", "", "", null, null);
}
