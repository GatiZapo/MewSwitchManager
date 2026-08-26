using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed record SwitchToolHealth(SwitchToolDefinition Definition, bool Installed, string Status, string? DetectedPath);

public sealed class SwitchToolValidator
{
    public IReadOnlyList<SwitchToolHealth> Validate(string sdRoot)
    {
        if (!Directory.Exists(sdRoot)) throw new DirectoryNotFoundException(sdRoot);
        return SwitchToolCatalog.Definitions.Select(x =>
        {
            var path = Path.Combine(sdRoot, x.Destination.Replace('/', Path.DirectorySeparatorChar));
            var exists = File.Exists(path) || Directory.Exists(path);
            return new SwitchToolHealth(x, exists, exists ? "Installed" : "Missing", exists ? path : null);
        }).ToArray();
    }

    public bool IsSafeTarget(string sdRoot)
    {
        if (!Directory.Exists(sdRoot)) return false;
        var full = Path.GetFullPath(sdRoot);
        return Directory.Exists(Path.Combine(full, "Nintendo")) || Directory.Exists(Path.Combine(full, "atmosphere")) || Directory.Exists(Path.Combine(full, "bootloader"));
    }
}
