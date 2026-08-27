using System.Security.Cryptography;

namespace MewNX.Core;

/// <summary>Compares persisted expectations with the actual target filesystem.</summary>
public sealed class StateReconciliationService
{
    public ReconciliationResult Reconcile(
        IEnumerable<string> expectedRelativePaths,
        string root)
    {
        ArgumentNullException.ThrowIfNull(expectedRelativePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (!Directory.Exists(root))
            return Unavailable();

        var missing = new List<string>();
        var invalid = new List<string>();
        var rootFull = NormalizeRoot(root);

        foreach (var relative in expectedRelativePaths.Where(static p => !string.IsNullOrWhiteSpace(p)))
        {
            if (!TryResolveInsideRoot(rootFull, relative, out var full))
            {
                invalid.Add(relative);
                continue;
            }

            if (!File.Exists(full))
                missing.Add(full);
            else if (new FileInfo(full).Length == 0)
                invalid.Add(full);
        }

        return CreateResult(missing, invalid,
            "{0} missing and {1} invalid expected file(s).");
    }

    public async Task<ReconciliationResult> ReconcileHashesAsync(
        IReadOnlyDictionary<string, string> expectedSha256,
        string root,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(expectedSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (!Directory.Exists(root))
            return Unavailable();

        var missing = new List<string>();
        var invalid = new List<string>();
        var rootFull = NormalizeRoot(root);

        foreach (var pair in expectedSha256)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryResolveInsideRoot(rootFull, pair.Key, out var full) || string.IsNullOrWhiteSpace(pair.Value))
            {
                invalid.Add(pair.Key);
                continue;
            }

            if (!File.Exists(full))
            {
                missing.Add(full);
                continue;
            }

            await using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));
            if (!string.Equals(actual, pair.Value.Trim(), StringComparison.OrdinalIgnoreCase))
                invalid.Add(full);
        }

        return CreateResult(missing, invalid,
            "One or more expected files failed integrity checks.");
    }

    private static string NormalizeRoot(string root)
        => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
           + Path.DirectorySeparatorChar;

    private static bool TryResolveInsideRoot(string rootFull, string relative, out string full)
    {
        full = string.Empty;
        if (string.IsNullOrWhiteSpace(relative)) return false;
        full = Path.GetFullPath(Path.Combine(rootFull, relative));
        return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
    }

    private static ReconciliationResult CreateResult(
        List<string> missing,
        List<string> invalid,
        string message)
    {
        var consistent = missing.Count == 0 && invalid.Count == 0;
        return new(consistent, missing, invalid,
            consistent ? null : message.Contains("{0}", StringComparison.Ordinal)
                ? string.Format(message, missing.Count, invalid.Count)
                : message);
    }

    private static ReconciliationResult Unavailable()
        => new(false, [], [], "Target directory is unavailable.");
}

public sealed record ReconciliationResult(
    bool IsConsistent,
    IReadOnlyList<string> MissingPaths,
    IReadOnlyList<string> InvalidPaths,
    string? Message);
