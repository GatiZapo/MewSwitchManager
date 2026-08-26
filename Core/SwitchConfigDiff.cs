namespace MewSwitchManager.Core;

public sealed record ConfigChange(string Path, string Kind, string? Before, string? After);

public sealed class SwitchConfigDiff
{
    private static readonly string[] ImportantFiles =
    [
        "bootloader/hekate_ipl.ini",
        "atmosphere/system_settings.ini",
        "atmosphere/config/system_settings.ini",
        "exosphere.ini"
    ];

    public IReadOnlyList<ConfigChange> Compare(string beforeRoot, string afterRoot)
    {
        var result = new List<ConfigChange>();
        foreach (var relative in ImportantFiles)
        {
            var before = Read(beforeRoot, relative);
            var after = Read(afterRoot, relative);
            if (before == after) continue;
            result.Add(new ConfigChange(relative, before is null ? "Added" : after is null ? "Removed" : "Changed", before, after));
        }
        return result;
    }

    public IReadOnlyList<string> GetProtectedFiles(string sdRoot)
        => ImportantFiles.Where(x => File.Exists(Path.Combine(sdRoot, x.Replace('/', Path.DirectorySeparatorChar)))).ToArray();

    private static string? Read(string root, string relative)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
