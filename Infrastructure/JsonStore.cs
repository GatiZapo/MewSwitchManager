using System.Text.Json;

namespace MewSwitchManager.Infrastructure;

public sealed class JsonStore<T> where T : class, new()
{
    private readonly string _path;
    private readonly string _backupPath;
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonStore(string path)
    {
        _path = path;
        _backupPath = path + ".bak";
    }

    public T LoadOrCreate()
    {
        lock (Sync)
        {
            if (TryLoad(_path, out var value)) return value!;
            if (TryLoad(_backupPath, out value)) return value!;

            TryQuarantineCorruptFile(_path);
            return new T();
        }
    }

    public void Save(T value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("State directory is invalid.");

        lock (Sync)
        {
            Directory.CreateDirectory(directory);
            var tmp = _path + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            var json = JsonSerializer.Serialize(value, Options);

            try
            {
                using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(_path))
                    File.Copy(_path, _backupPath, overwrite: true);

                File.Move(tmp, _path, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }

    private static bool TryLoad(string path, out T? value)
    {
        value = null;
        try
        {
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            value = JsonSerializer.Deserialize<T>(json, Options);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void TryQuarantineCorruptFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var backup = path + $".corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            File.Move(path, backup, overwrite: false);
        }
        catch { }
    }
}
