using System.Text.Json;
using MewNX.Infrastructure;

namespace MewNX.Core;

public sealed record EmummcInfo(bool Present, string Type, string? Id, string? Sector, string Message);

public sealed class EmummcManager
{
    private readonly AppLogger _logger;
    public EmummcManager(AppLogger logger) { _logger = logger; }

    public EmummcInfo Inspect(string sdRoot)
    {
        if (!Directory.Exists(sdRoot)) throw new DirectoryNotFoundException(sdRoot);
        var config = Path.Combine(sdRoot, "emuMMC", "emummc.ini");
        if (!File.Exists(config)) return new(false, "None", null, null, "No emuMMC configuration detected.");
        var values = File.ReadAllLines(config).Where(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith("#"))
            .Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0].Trim(), x => x[1].Trim(), StringComparer.OrdinalIgnoreCase);
        values.TryGetValue("type", out var type); values.TryGetValue("id", out var id); values.TryGetValue("sector", out var sector);
        return new(true, type ?? "Unknown", id, sector, $"emuMMC detected: type={type ?? "Unknown"}, id={id ?? "-"}.");
    }

    public string BackupFileBased(string sdRoot, string destination)
    {
        var source = Path.Combine(sdRoot, "emuMMC");
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("emuMMC directory not found.");
        Directory.CreateDirectory(destination);
        CopyDirectory(source, Path.Combine(destination, "emuMMC"));
        File.WriteAllText(Path.Combine(destination, "backup-info.json"), JsonSerializer.Serialize(new { createdUtc = DateTimeOffset.UtcNow, source, kind = "file-based-emummc-metadata-and-files" }, new JsonSerializerOptions { WriteIndented = true }));
        _logger.Info($"File-based emuMMC backup created: {destination}");
        return destination;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
