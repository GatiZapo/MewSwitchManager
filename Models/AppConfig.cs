namespace MewSwitchManager.Models;

public sealed class AppConfig
{
    public string AppVersion { get; set; } = "0.3.0-alpha";
    public LinuxImageConfig LinuxImage { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
    public SafetyConfig Safety { get; set; } = new();
    public DependencyConfig Dependencies { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
    public UpdateConfig Updates { get; set; } = new();
}

public sealed class LinuxImageConfig
{
    public string Url { get; set; } = "https://download.switchroot.org/ubuntu-noble/theofficialgman-kubuntu-noble-5.1.2-2026-05-13.7z";
    public string FileName { get; set; } = "theofficialgman-kubuntu-noble-5.1.2-2026-05-13.7z";
    public string Sha1 { get; set; } = "66e6d4990df4d671e64615b7ec6276299fdebc21";
    public long ExpectedSizeBytes { get; set; } = 2374622000L;
    public string LinuxDistroVersion { get; set; } = "Ubuntu Noble / Switchroot 5.1.2";
}

public sealed class StorageConfig
{
    public string CacheDirectory { get; set; } = "MewSwitch\\Cache";
    public string StateFile { get; set; } = "MewSwitch\\state.json";
}

public sealed class SafetyConfig
{
    public bool AllowDestructiveOperations { get; set; } = true;
    public bool RequireExplicitConfirmation { get; set; } = true;
    public bool RequireAdministrator { get; set; } = true;
}

public sealed class DependencyConfig
{
    public bool AutoInstallMissing { get; set; } = false;
    public bool InstallWslIfMissing { get; set; } = false;
}

public sealed class UiConfig
{
    public string Accent { get; set; } = "Mew";
    public bool StartMaximized { get; set; } = false;
    public bool RememberWindowBounds { get; set; } = true;
}

public sealed class UpdateConfig
{
    public bool Enabled { get; set; } = true;
    public string Repository { get; set; } = "GatiZapo/MewSwitchManager";
    public bool CheckOnStartup { get; set; } = true;
}
