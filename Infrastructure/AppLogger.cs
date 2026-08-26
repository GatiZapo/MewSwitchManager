namespace MewSwitchManager.Infrastructure;

/// <summary>
/// Central application logger. Every UI-visible message is also persisted so a failed
/// operation can be diagnosed after the application closes.
/// </summary>
public sealed class AppLogger
{
    private readonly string _file;
    private readonly object _gate = new();
    public event Action<string>? Message;

    public AppLogger(string file)
    {
        _file = file;
        try
        {
            var directory = Path.GetDirectoryName(_file);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        }
        catch { }
        Info($"MewNX session started. Log: {_file}");
    }

    public void Debug(string text) => Write("DEBUG", text, notify: false);
    public void Info(string text) => Write("INFO", text);
    public void Warn(string text) => Write("WARN", text);
    public void Error(string text, Exception? ex = null)
        => Write("ERROR", ex is null ? text : $"{text}: {ex}", notify: true);

    private void Write(string level, string text, bool notify = true)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{level}] {text}";
        lock (_gate)
        {
            try { File.AppendAllText(_file, line + Environment.NewLine); }
            catch { }
        }
        if (notify) Message?.Invoke(line);
    }
}
