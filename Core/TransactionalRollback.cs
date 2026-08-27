namespace MewSwitchManager.Core;

public sealed class TransactionalRollback : IDisposable
{
    private readonly string _root;
    private readonly Dictionary<string, string?> _originals = new(StringComparer.OrdinalIgnoreCase);
    private bool _committed;

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
        var backup = Path.Combine(_root, _originals.Count.ToString("D8") + ".bak");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.Copy(full, backup, true);
        _originals[full] = backup;
    }

    public void Commit() => _committed = true;

    public void Rollback()
    {
        if (_committed) return;
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
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
