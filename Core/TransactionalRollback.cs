namespace MewSwitchManager.Core;

/// <summary>
/// File-system transaction journal used by managed component updates.
/// A transaction can snapshot individual files or an entire component directory.
/// Rollback restores overwritten files and removes files/directories created after capture.
/// </summary>
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

    /// <summary>
    /// Captures the complete contents of a directory. If the directory does not exist,
    /// rollback removes the directory if the transaction creates it later.
    /// </summary>
    public void CaptureDirectory(string directoryPath)
    {
        var full = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (_capturedDirectories.ContainsKey(full)) return;

        var existed = Directory.Exists(full);
        _capturedDirectories[full] = existed;

        if (!existed) return;

        foreach (var directory in Directory.EnumerateDirectories(full, "*", SearchOption.AllDirectories))
            _originalDirectories.Add(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
            Capture(file);
    }

    public void Commit() => _committed = true;

    public void Rollback()
    {
        if (_committed || _rolledBack) return;
        _rolledBack = true;

        // Remove files created after directory capture first.
        foreach (var capturedDirectory in _capturedDirectories)
        {
            try
            {
                if (!capturedDirectory.Value)
                {
                    if (Directory.Exists(capturedDirectory.Key)) Directory.Delete(capturedDirectory.Key, true);
                    continue;
                }

                if (!Directory.Exists(capturedDirectory.Key)) continue;
                foreach (var file in Directory.EnumerateFiles(capturedDirectory.Key, "*", SearchOption.AllDirectories))
                {
                    var full = Path.GetFullPath(file);
                    if (!_originals.ContainsKey(full))
                        File.Delete(full);
                }

                foreach (var directory in Directory.EnumerateDirectories(capturedDirectory.Key, "*", SearchOption.AllDirectories)
                             .OrderByDescending(x => x.Length))
                {
                    var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!_originalDirectories.Contains(full) && !Directory.EnumerateFileSystemEntries(full).Any())
                        Directory.Delete(full, false);
                }
            }
            catch { }
        }

        foreach (var pair in _originals.Reverse())
        {
            try
            {
                if (pair.Value is null)
                {
                    if (File.Exists(pair.Key)) File.Delete(pair.Key);
                    continue;
                }

                var directory = Path.GetDirectoryName(pair.Key);
                if (string.IsNullOrWhiteSpace(directory)) continue;
                Directory.CreateDirectory(directory);
                File.Copy(pair.Value, pair.Key, true);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (!_committed) Rollback();
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            var parent = Directory.GetParent(_root)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                Directory.Delete(parent, false);
        }
        catch { }
    }
}
