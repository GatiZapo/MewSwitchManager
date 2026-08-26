using System.Text;
using MewSwitchManager.Infrastructure;

namespace MewSwitchManager.Core;

public sealed class SwitchConfigManager
{
    private readonly AppPaths _paths;
    private readonly AppLogger _logger;

    public SwitchConfigManager(AppPaths paths, AppLogger logger) { _paths = paths; _logger = logger; }

    public string Backup(string sdRoot)
    {
        if (!Directory.Exists(sdRoot)) throw new DirectoryNotFoundException(sdRoot);
        var backup = Path.Combine(_paths.DataDirectory, "switch-config-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backup);
        CopyIfPresent(sdRoot, Path.Combine("bootloader", "hekate_ipl.ini"), backup);
        CopyIfPresent(sdRoot, Path.Combine("atmosphere", "config"), backup);
        CopyIfPresent(sdRoot, Path.Combine("atmosphere", "hosts"), backup);
        CopyIfPresent(sdRoot, "exosphere.ini", backup);
        File.WriteAllText(Path.Combine(backup, "README.txt"), "MewSwitch Manager configuration snapshot\n" + DateTimeOffset.UtcNow + "\n", Encoding.UTF8);
        _logger.Info($"Switch configuration backup created: {backup}");
        return backup;
    }

    public void Restore(string sdRoot, string backupDirectory)
    {
        if (!Directory.Exists(sdRoot)) throw new DirectoryNotFoundException(sdRoot);
        if (!Directory.Exists(backupDirectory)) throw new DirectoryNotFoundException(backupDirectory);
        foreach (var file in Directory.GetFiles(backupDirectory, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).Equals("README.txt", StringComparison.OrdinalIgnoreCase)) continue;
            var relative = Path.GetRelativePath(backupDirectory, file);
            var destination = Path.GetFullPath(Path.Combine(sdRoot, relative));
            var root = Path.GetFullPath(sdRoot) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid configuration backup path.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
        _logger.Info($"Switch configuration restored from: {backupDirectory}");
    }

    private static void CopyIfPresent(string root, string relative, string backup)
    {
        var source = Path.Combine(root, relative);
        var destination = Path.Combine(backup, relative);
        if (File.Exists(source)) { Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true); return; }
        if (!Directory.Exists(source)) return;
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
