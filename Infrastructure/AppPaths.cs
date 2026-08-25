using MewSwitchManager.Models;

namespace MewSwitchManager.Infrastructure;

public sealed record AppPaths(string DataDirectory, string CacheDirectory, string StateFile, string LogFile)
{
    public static AppPaths Create(AppConfig config)
    {
        // The image cache can be several gigabytes, so it belongs in LocalAppData
        // rather than a roaming profile. Small state/log files stay in AppData.
        var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var appDirectory = Path.Combine(roamingRoot, "MewSwitch");
        var cacheDirectory = Resolve(localRoot, config.Storage.CacheDirectory, Path.Combine(localRoot, "MewSwitch", "Cache"));
        var stateFile = Resolve(roamingRoot, config.Storage.StateFile, Path.Combine(appDirectory, "state.json"));
        var logFile = Path.Combine(appDirectory, "mewswitch.log");

        return new AppPaths(appDirectory, cacheDirectory, stateFile, logFile);
    }

    private static string Resolve(string root, string configured, string fallback)
    {
        if (string.IsNullOrWhiteSpace(configured)) return fallback;
        return Path.IsPathRooted(configured) ? configured : Path.Combine(root, configured);
    }
}
