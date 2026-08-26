using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed record ToolRecommendation(string ToolId, string Reason, bool Recommended);

public sealed class SwitchCheckpoint
{
    private readonly AppPaths _paths;
    private readonly AppLogger _logger;
    public SwitchCheckpoint(AppPaths paths, AppLogger logger) { _paths = paths; _logger = logger; }

    public string Create(string sdRoot, string reason)
    {
        if (!Directory.Exists(sdRoot)) throw new DirectoryNotFoundException(sdRoot);
        var root = Path.Combine(_paths.DataDirectory, "checkpoints", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(root);
        foreach (var relative in new[] { "bootloader/hekate_ipl.ini", "exosphere.ini", "atmosphere/config", "atmosphere/hosts", "emuMMC/emummc.ini" })
            CopyIfPresent(sdRoot, relative, root);
        File.WriteAllText(Path.Combine(root, "reason.txt"), reason + Environment.NewLine + DateTimeOffset.UtcNow);
        _logger.Info($"Checkpoint created: {root}");
        return root;
    }

    public IReadOnlyList<ToolRecommendation> Recommend(SwitchSdReport report)
    {
        var list = new List<ToolRecommendation>();
        if (!report.HasHekate) list.Add(new("tegraexplorer", "Hekate payload maintenance is not detected.", true));
        if (report.HasAtmosphere) list.Add(new("checkpoint", "Useful for save-data backups before major changes.", true));
        if (report.HasAtmosphere) list.Add(new("jksv", "Useful save-data management companion.", true));
        list.Add(new("status-monitor", "Useful for diagnosing performance and thermals.", false));
        return list;
    }

    private static void CopyIfPresent(string root, string relative, string destinationRoot)
    {
        var normalized = relative.Replace('/', Path.DirectorySeparatorChar);
        var source = Path.Combine(root, normalized);
        if (File.Exists(source))
        {
            var target = Path.Combine(destinationRoot, normalized);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);
            return;
        }
        if (!Directory.Exists(source)) return;
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destinationRoot, normalized, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
