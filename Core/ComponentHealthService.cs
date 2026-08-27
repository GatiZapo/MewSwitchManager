using System.Security.Cryptography;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public enum ComponentHealthSeverity { Healthy, Warning, Broken }

public sealed record ComponentHealthCheck(
    string ComponentId,
    ComponentHealthSeverity Severity,
    string Title,
    string Message,
    string? ExpectedSha256 = null,
    string? ActualSha256 = null);

/// <summary>
/// Performs read-only integrity checks over managed SD-card component areas.
/// It never repairs or writes files; RepairService is deliberately separate.
/// </summary>
public sealed class ComponentHealthService
{
    private static readonly IReadOnlyDictionary<SwitchComponent, (string Name, string Detection)> Definitions =
        new Dictionary<SwitchComponent, (string, string)>
        {
            [SwitchComponent.Hekate] = ("Hekate / Nyx", "bootloader/update.bin"),
            [SwitchComponent.Atmosphere] = ("Atmosphère", "atmosphere/package3"),
            [SwitchComponent.Dbi] = ("DBI", "switch/DBI/DBI.nro")
        };

    public async Task<IReadOnlyList<ComponentHealthCheck>> ScanAsync(
        string targetRoot,
        IReadOnlyDictionary<string, string>? expectedHashes = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(targetRoot)) throw new DirectoryNotFoundException(targetRoot);

        var results = new List<ComponentHealthCheck>();
        foreach (var pair in Definitions)
        {
            ct.ThrowIfCancellationRequested();
            var path = Path.Combine(targetRoot, pair.Value.Detection.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                results.Add(new(pair.Key.ToString(), ComponentHealthSeverity.Broken, pair.Value.Name,
                    $"Required file is missing: {pair.Value.Detection}"));
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length == 0)
            {
                results.Add(new(pair.Key.ToString(), ComponentHealthSeverity.Broken, pair.Value.Name,
                    $"Required file is empty: {pair.Value.Detection}"));
                continue;
            }

            string? expected = null;
            string? actual = null;
            if (expectedHashes is not null && expectedHashes.TryGetValue(pair.Key.ToString(), out var configured))
            {
                expected = NormalizeHash(configured);
                actual = await ComputeSha256Async(path, ct);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new(pair.Key.ToString(), ComponentHealthSeverity.Broken, pair.Value.Name,
                        "Integrity hash does not match the expected catalog hash.", expected, actual));
                    continue;
                }
            }

            results.Add(new(pair.Key.ToString(), ComponentHealthSeverity.Healthy, pair.Value.Name,
                expected is null ? "Required payload exists and is non-empty." : "Required payload exists and matches its catalog hash.", expected, actual));
        }

        return results;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        return Convert.ToHexString(await sha.ComputeHashAsync(stream, ct));
    }

    private static string NormalizeHash(string value)
        => value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : value.Trim();
}
