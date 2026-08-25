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
    string? AssetName,
    string? ErrorMessage = null)
{
    public static UpdateInfo NoUpdate(string current, string latest = "") =>
        new(false, current, latest, "", "", "", "", null, null, null);

    public static UpdateInfo Error(string current, string message) =>
        new(false, current, "", "", "", "", "", null, null, message);
}
