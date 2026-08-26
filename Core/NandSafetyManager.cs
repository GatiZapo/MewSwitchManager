using System.Security.Cryptography;
using System.Text.Json;
using MewSwitchManager.Infrastructure;

namespace MewSwitchManager.Core;

public sealed record NandBackupPlan(bool IsSafe, string Reason, string Destination, IReadOnlyList<string> RequiredFiles);
public sealed record NandBackupManifest(DateTimeOffset CreatedUtc, string Source, string Destination, IReadOnlyDictionary<string,string> Sha256);

public sealed class NandSafetyManager
{
    private readonly AppLogger _logger;
    public NandSafetyManager(AppLogger logger) { _logger = logger; }

    public NandBackupPlan Plan(string destination)
    {
        var full = Path.GetFullPath(destination);
        var required = new[] { "BOOT0.bin", "BOOT1.bin", "rawnand.bin" };
        return new NandBackupPlan(
            !string.IsNullOrWhiteSpace(full) && Directory.Exists(Path.GetDirectoryName(full) ?? full),
            "A physical NAND dump must be produced by a trusted RCM/Hekate workflow; MewSwitch never reads raw eMMC sectors directly from Windows.",
            full,
            required);
    }

    public async Task<NandBackupManifest> VerifyDumpDirectoryAsync(string directory, CancellationToken ct = default)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        var hashes = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(directory, "*.bin", SearchOption.TopDirectoryOnly))
        {
            await using var stream = File.OpenRead(file);
            using var sha = SHA256.Create();
            hashes[Path.GetFileName(file)] = Convert.ToHexString(await sha.ComputeHashAsync(stream, ct));
        }
        var manifest = new NandBackupManifest(DateTimeOffset.UtcNow, "Hekate/RCM dump", Path.GetFullPath(directory), hashes);
        await File.WriteAllTextAsync(Path.Combine(directory, "mewswitch-nand-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), ct);
        _logger.Info($"NAND backup directory verified and manifest created: {directory}");
        return manifest;
    }
}
