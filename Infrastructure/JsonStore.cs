using System.Text.Json;

namespace MewNX.Infrastructure;

public sealed class JsonStore<T> where T : class, new()
{
    private readonly string _path;
    public string Path => _path;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonStore(string path) => _path = path;

    public T LoadOrCreate()
    {
        try
        {
            if (!File.Exists(_path)) return new T();
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path), Options) ?? new T();
        }
        catch
        {
            try
            {
                var backup = _path + $".corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
                File.Move(_path, backup, overwrite: false);
            }
            catch { }
            return new T();
        }
    }

    public void Save(T value)
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("State directory is invalid.");
        Directory.CreateDirectory(directory);

        var tmp = _path + $".{Environment.ProcessId}.tmp";
        var json = JsonSerializer.Serialize(value, Options);
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }
}
