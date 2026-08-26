using MewSwitchManager.Models;

namespace MewSwitchManager.Infrastructure;

public sealed record AppPaths(string DataDirectory, string CacheDirectory, string StateFile, string LogFile)
{
    public static AppPaths Create(AppConfig config)
    {
        // Large image caches stay in LocalAppData. State and diagnostics live in the
        // MewNX roaming directory so the product name is consistent everywhere.
        var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var appDirectory = Path.Combine(roamingRoot, "MewNX");
        var cacheDirectory = Resolve(localRoot, config.Storage.CacheDirectory, Path.Combine(localRoot, "MewNX", "Cache"));
        var stateFile = Resolve(roamingRoot, config.Storage.StateFile, Path.Combine(appDirectory, "state.json"));
        var logFile = Path.Combine(appDirectory, "mewnx.log");

        return new AppPaths(appDirectory, cacheDirectory, stateFile, logFile);
    }

    private static string Resolve(string root, string configured, string fallback)
    {
        if (string.IsNullOrWhiteSpace(configured)) return fallback;
        return Path.IsPathRooted(configured) ? configured : Path.Combine(root, configured);
    }
}
