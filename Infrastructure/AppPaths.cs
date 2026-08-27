using MewNX.Models;

namespace MewNX.Infrastructure;

public sealed record AppPaths(string DataDirectory, string CacheDirectory, string StateFile, string LogFile)
{
    public static AppPaths Create(AppConfig config)
    {
        var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDirectory = Path.Combine(roamingRoot, "MewNX");
        var legacyDirectory = Path.Combine(roamingRoot, "MewSwitch");

        Directory.CreateDirectory(appDirectory);
        var cacheDirectory = Resolve(localRoot, config.Storage.CacheDirectory, Path.Combine(localRoot, "MewNX", "Cache"));
        var stateFile = Resolve(roamingRoot, config.Storage.StateFile, Path.Combine(appDirectory, "state.json"));

        MigrateLegacyState(legacyDirectory, stateFile);
        MigrateLegacyCache(localRoot, cacheDirectory);

        var logFile = Path.Combine(appDirectory, "mewnx.log");
        return new AppPaths(appDirectory, cacheDirectory, stateFile, logFile);
    }

    private static string Resolve(string root, string configured, string fallback)
    {
        if (string.IsNullOrWhiteSpace(configured)) return fallback;
        return Path.IsPathRooted(configured) ? configured : Path.Combine(root, configured);
    }

    private static void MigrateLegacyState(string legacyDirectory, string newStateFile)
    {
        if (File.Exists(newStateFile)) return;
        var legacyState = Path.Combine(legacyDirectory, "state.json");
        try
        {
            if (File.Exists(legacyState))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newStateFile)!);
                File.Copy(legacyState, newStateFile, overwrite: false);
            }
        }
        catch { }
    }

    private static void MigrateLegacyCache(string localRoot, string newCacheDirectory)
    {
        if (Directory.Exists(newCacheDirectory)) return;
        var legacyCache = Path.Combine(localRoot, "MewSwitch", "Cache");
        if (!Directory.Exists(legacyCache)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newCacheDirectory)!);
            Directory.Move(legacyCache, newCacheDirectory);
        }
        catch { }
    }
}
