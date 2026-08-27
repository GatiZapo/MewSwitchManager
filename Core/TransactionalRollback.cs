using System.Security.Cryptography;

namespace MewNX.Core;

/// <summary>Snapshots managed filesystem targets and restores them on transaction failure.</summary>
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
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        _root = Path.Combine(Path.GetFullPath(workingDirectory), "_mewnx-transactions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Capture(string targetPath)
    {
        var full = Path.GetFullPath(targetPath);
        if (_originals.ContainsKey(full)) return;

        if (!File.Exists(full))
        {
            _originals[full] = null;
            return;
        }

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

        _originalDirectories.Add(full);
        foreach (var directory in Directory.EnumerateDirectories(full, "*", SearchOption.AllDirectories))
            _originalDirectories.Add(NormalizeDirectory(directory));

        foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
            Capture(file);
    }

    public void Commit()
    {
        if (_rolledBack) throw new InvalidOperationException("A rolled-back transaction cannot be committed.");
        _committed = true;
    }

    public void Rollback()
    {
        if (_committed) throw new InvalidOperationException("A committed transaction cannot be rolled back.");
        if (_rolledBack) return;
        _rolledBack = true;

        var failures = new List<Exception>();

        foreach (var capturedDirectory in _capturedDirectories)
        {
            try
            {
                RestoreDirectoryStructure(capturedDirectory.Key, capturedDirectory.Value);
            }
            catch (Exception ex)
            {
                failures.Add(new IOException($"Failed to restore directory '{capturedDirectory.Key}'.", ex));
            }
        }

        foreach (var pair in _originals.Reverse())
        {
            try
            {
                RestoreFile(pair.Key, pair.Value);
            }
            catch (Exception ex)
            {
                failures.Add(new IOException($"Failed to restore file '{pair.Key}'.", ex));
            }
        }

        if (failures.Count > 0)
            throw new AggregateException("One or more rollback operations failed.", failures);
    }

    public bool VerifyRestoredState()
    {
        if (!_rolledBack || _committed) return false;

        foreach (var pair in _originals)
        {
            if (pair.Value is null)
            {
                if (File.Exists(pair.Key)) return false;
                continue;
            }

            if (!File.Exists(pair.Key) || !File.Exists(pair.Value)) return false;
            if (new FileInfo(pair.Key).Length != new FileInfo(pair.Value).Length) return false;
            if (!HashesEqual(pair.Key, pair.Value)) return false;
        }

        foreach (var capturedDirectory in _capturedDirectories)
        {
            if (capturedDirectory.Value != Directory.Exists(capturedDirectory.Key)) return false;
            if (!capturedDirectory.Value) continue;

            foreach (var file in Directory.EnumerateFiles(capturedDirectory.Key, "*", SearchOption.AllDirectories))
                if (!_originals.ContainsKey(Path.GetFullPath(file))) return false;

            foreach (var directory in Directory.EnumerateDirectories(capturedDirectory.Key, "*", SearchOption.AllDirectories))
                if (!_originalDirectories.Contains(NormalizeDirectory(directory))) return false;
        }

        return true;
    }

    private void RestoreDirectoryStructure(string directory, bool existed)
    {
        if (!existed)
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            return;
        }

        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToArray())
            if (!_originals.ContainsKey(Path.GetFullPath(file))) File.Delete(file);

        foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static path => path.Length).ToArray())
        {
            var full = NormalizeDirectory(child);
            if (!_originalDirectories.Contains(full) && !Directory.EnumerateFileSystemEntries(full).Any())
                Directory.Delete(full, false);
        }
    }

    private static void RestoreFile(string target, string? backup)
    {
        if (backup is null)
        {
            if (File.Exists(target)) File.Delete(target);
            return;
        }

        var directory = Path.GetDirectoryName(target);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("Target has no parent directory.");

        Directory.CreateDirectory(directory);
        File.Copy(backup, target, true);
    }

    private static string NormalizeDirectory(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool HashesEqual(string left, string right)
    {
        using var sha = SHA256.Create();
        using var a = File.OpenRead(left);
        using var b = File.OpenRead(right);
        return CryptographicOperations.FixedTimeEquals(sha.ComputeHash(a), sha.ComputeHash(b));
    }

    public void Dispose()
    {
        if (!_committed && !_rolledBack)
        {
            try { Rollback(); } catch { /* Best-effort cleanup during disposal. */ }
        }

        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            var parent = Directory.GetParent(_root)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                Directory.Delete(parent, false);
        }
        catch { /* Cleanup must not mask the operation result. */ }
    }
}
