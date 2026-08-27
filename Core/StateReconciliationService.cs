using System.Security.Cryptography;

namespace MewSwitchManager.Core;

/// <summary>Compares persisted expectations with the actual target filesystem.</summary>
public sealed class StateReconciliationService
{
    public ReconciliationResult Reconcile(IEnumerable<string> expectedRelativePaths, string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return new(false, Array.Empty<string>(), Array.Empty<string>(), "Target directory is unavailable.");
        var missing = new List<string>();
        var invalid = new List<string>();
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var relative in expectedRelativePaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) { invalid.Add(relative); continue; }
            if (!File.Exists(full)) missing.Add(full);
            else if (new FileInfo(full).Length == 0) invalid.Add(full);
        }
        return new(missing.Count == 0 && invalid.Count == 0, missing, invalid,
            missing.Count == 0 && invalid.Count == 0 ? null : $"{missing.Count} missing and {invalid.Count} invalid expected file(s).");
    }

    public async Task<ReconciliationResult> ReconcileHashesAsync(IReadOnlyDictionary<string, string> expectedSha256, string root, CancellationToken ct = default)
    {
        var missing = new List<string>();
        var invalid = new List<string>();
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var pair in expectedSha256)
        {
            var full = Path.GetFullPath(Path.Combine(root, pair.Key));
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) { invalid.Add(pair.Key); continue; }
            if (!File.Exists(full)) { missing.Add(full); continue; }
            await using var stream = File.OpenRead(full);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            if (!string.Equals(actual, pair.Value.Trim(), StringComparison.OrdinalIgnoreCase)) invalid.Add(full);
        }
        return new(missing.Count == 0 && invalid.Count == 0, missing, invalid,
            missing.Count == 0 && invalid.Count == 0 ? null : "One or more expected files failed integrity checks.");
    }
}

public sealed record ReconciliationResult(bool IsConsistent, IReadOnlyList<string> MissingPaths, IReadOnlyList<string> InvalidPaths, string? Message);
