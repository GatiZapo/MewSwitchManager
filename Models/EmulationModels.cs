namespace MewSwitchManager.Models;

public enum EmulationSourceKind
{
    GitHubRelease,
    OfficialBundle
}

public enum EmulationInstallMode
{
    DirectFile,
    ArchiveToRoot
}

public sealed record EmulationPackageDefinition(
    string Id,
    string Name,
    string Systems,
    EmulationSourceKind SourceKind,
    string Repository,
    string AssetName,
    string Destination,
    EmulationInstallMode InstallMode,
    bool RequiredForFullStack,
    string Description,
    IReadOnlyList<string>? PreserveRelativePaths = null)
{
    public override string ToString() => Name;
}

public sealed record EmulationInstallResult(
    EmulationPackageDefinition Definition,
    string Version,
    string BackupPath,
    string Message);
