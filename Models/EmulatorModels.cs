namespace MewSwitchManager.Models;

public enum EmulatorDistribution
{
    GitHubRelease,
    OfficialBuildbot,
    ManualOnly
}

public sealed record EmulatorDefinition(
    string Id,
    string Name,
    string Systems,
    string Repository,
    EmulatorDistribution Distribution,
    string InstallRoot,
    string ExpectedPath,
    string AssetPattern,
    bool Recommended,
    string Description,
    string Notes = "")
{
    public override string ToString() => Name;
}

public sealed record EmulatorInstallStatus(
    EmulatorDefinition Definition,
    bool Installed,
    string InstalledVersion,
    string AvailableVersion,
    bool UpdateAvailable,
    string Message);
