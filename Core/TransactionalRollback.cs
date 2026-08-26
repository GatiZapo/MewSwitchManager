namespace MewSwitchManager.Core;

public sealed class TransactionalRollback : IDisposable
{
    private readonly string _root;
    private readonly Dictionary<string, string?> _originals = new(StringComparer.OrdinalIgnoreCase);
    private bool _committed;

    public TransactionalRollback(string workingDirectory)
    {
        _root = Path.Combine(workingDirectory, "_mewnx-transactions", DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(_root);
    }

    public void Capture(string targetPath)
    {
        var full = Path.GetFullPath(targetPath);
        if (_originals.ContainsKey(full)) return;
        if (!File.Exists(full)) { _originals[full] = null; return; }
        var backup = Path.Combine(_root, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(full))) + ".bak");
        File.Copy(full, backup, true);
        _originals[full] = backup;
    }

    public void Commit() { _committed = true; }

    public void Rollback()
    {
        if (_committed) return;
        foreach (var pair in _originals)
        {
            if (pair.Value is null)
            {
                TryDelete(pair.Key);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(pair.Key)!);
            File.Copy(pair.Value, pair.Key, true);
        }
    }

    public void Dispose()
    {
        if (!_committed) Rollback();
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
