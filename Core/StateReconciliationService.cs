namespace MewSwitchManager.Core;

/// <summary>Compares persisted paths with the current target and reports whether state can be trusted.</summary>
public sealed class StateReconciliationService
{
    public ReconciliationResult Reconcile(IEnumerable<string> expectedRelativePaths, string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return new(false, Array.Empty<string>(), "Target directory is unavailable.");

        var missing = expectedRelativePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.Combine(root, p))
            .Where(p => !File.Exists(p))
            .Select(Path.GetFullPath)
            .ToArray();

        return missing.Length == 0
            ? new(true, missing, null)
            : new(false, missing, $"{missing.Length} expected file(s) are missing.");
    }
}

public sealed record ReconciliationResult(bool IsConsistent, IReadOnlyList<string> MissingPaths, string? Message);
