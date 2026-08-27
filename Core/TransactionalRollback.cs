using System.Security.Cryptography;

namespace MewSwitchManager.Core;

public sealed record RollbackResult(bool Success, IReadOnlyList<string> Errors)
{
    public static RollbackResult Ok() => new(true, Array.Empty<string>());
}

public sealed class TransactionalRollback : IDisposable
{
    private readonly string _root;
    private readonly Dictionary<string, string?> _originals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _capturedDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _originalDirectories = new(StringComparer.OrdinalIgnoreCase);
    private bool _committed;
    private bool _rolledBack;

    public TransactionalRollback(string workingDirectory)
    {
        _root = Path.Combine(workingDirectory, "_mewnx-transactions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Capture(string targetPath)
    {
        var full = Path.GetFullPath(targetPath);
        if (_originals.ContainsKey(full)) return;
        if (!File.Exists(full)) { _originals[full] = null; return; }
        var backup = Path.Combine(_root, "files", _originals.Count.ToString("D8") + ".bak");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.Copy(full, backup, true);
        _originals[full] = backup;
    }

    public void CaptureDirectory(string directoryPath)
    {
        var full = NormalizeDirectory(directoryPath);
        if (_capturedDirectories.ContainsKey(full)) return;
        var existed = Directory.Exists(full);
        _capturedDirectories[full] = existed;
        if (!existed) return;
        foreach (var directory in Directory.EnumerateDirectories(full, "*", SearchOption.AllDirectories)) _originalDirectories.Add(NormalizeDirectory(directory));
        foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories)) Capture(file);
    }

    public void Commit() => _committed = true;

    public RollbackResult Rollback()
    {
        if (_committed) return new(false, new[] { "Transaction has already been committed." });
        if (_rolledBack) return VerifyRestoredState();
        _rolledBack = true;
        var errors = new List<string>();
        foreach (var capturedDirectory in _capturedDirectories)
        {
            try
            {
                if (!capturedDirectory.Value)
                {
                    if (Directory.Exists(capturedDirectory.Key)) Directory.Delete(capturedDirectory.Key, true);
                    continue;
                }
                if (!Directory.Exists(capturedDirectory.Key)) { Directory.CreateDirectory(capturedDirectory.Key); continue; }
                foreach (var file in Directory.EnumerateFiles(capturedDirectory.Key, "*", SearchOption.AllDirectories))
                {
                    var full = Path.GetFullPath(file);
                    if (!_originals.ContainsKey(full)) File.Delete(full);
                }
                foreach (var directory in Directory.EnumerateDirectories(capturedDirectory.Key, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
                {
                    var full = NormalizeDirectory(directory);
                    if (!_originalDirectories.Contains(full) && !Directory.EnumerateFileSystemEntries(full).Any()) Directory.Delete(full, false);
                }
            }
            catch (Exception ex) { errors.Add($"Directory cleanup failed for {capturedDirectory.Key}: {ex.Message}"); }
        }
        foreach (var pair in _originals.Reverse())
        {
            try
            {
                if (pair.Value is null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); continue; }
                var directory = Path.GetDirectoryName(pair.Key);
                if (string.IsNullOrWhiteSpace(directory)) { errors.Add($"Invalid rollback path: {pair.Key}"); continue; }
                Directory.CreateDirectory(directory);
                File.Copy(pair.Value, pair.Key, true);
            }
            catch (Exception ex) { errors.Add($"File restore failed for {pair.Key}: {ex.Message}"); }
        }
        var verification = VerifyRestoredState();
        if (!verification.Success) errors.AddRange(verification.Errors);
        return errors.Count == 0 ? RollbackResult.Ok() : new RollbackResult(false, errors.Distinct(StringComparer.Ordinal).ToArray());
    }

    public void RollbackOrThrow()
    {
        var result = Rollback();
        if (!result.Success) throw new IOException("Transactional rollback could not be verified: " + string.Join(" | ", result.Errors));
    }

    private RollbackResult VerifyRestoredState()
    {
        var errors = new List<string>();
        foreach (var pair in _originals)
        {
            if (pair.Value is null) { if (File.Exists(pair.Key)) errors.Add($"New file still exists after rollback: {pair.Key}"); continue; }
            if (!File.Exists(pair.Key)) { errors.Add($"Original file is missing after rollback: {pair.Key}"); continue; }
            try { if (!FilesEqual(pair.Value, pair.Key)) errors.Add($"Restored file differs from its snapshot: {pair.Key}"); }
            catch (Exception ex) { errors.Add($"Could not verify restored file {pair.Key}: {ex.Message}"); }
        }
        foreach (var directory in _capturedDirectories)
        {
            if (directory.Value) { if (!Directory.Exists(directory.Key)) errors.Add($"Original directory is missing after rollback: {directory.Key}"); }
            else if (Directory.Exists(directory.Key)) errors.Add($"New directory still exists after rollback: {directory.Key}");
        }
        return errors.Count == 0 ? RollbackResult.Ok() : new RollbackResult(false, errors.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool FilesEqual(string left, string right)
    {
        var leftInfo = new FileInfo(left); var rightInfo = new FileInfo(right);
        if (leftInfo.Length != rightInfo.Length) return false;
        using var a = File.OpenRead(left); using var b = File.OpenRead(right); using var sha = SHA256.Create();
        var ah = sha.ComputeHash(a); sha.Initialize(); var bh = sha.ComputeHash(b);
        return CryptographicOperations.FixedTimeEquals(ah, bh);
    }

    private static string NormalizeDirectory(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public void Dispose()
    {
        if (!_committed && !_rolledBack) Rollback();
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            var parent = Directory.GetParent(_root)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent, false);
        }
        catch { }
    }
}
