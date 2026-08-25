namespace MewSwitchManager.Infrastructure;

public sealed class AppLogger
{
    private readonly string _file;
    private readonly object _gate = new();
    public event Action<string>? Message;

    public AppLogger(string file) => _file = file;

    public void Info(string text) => Write("INFO", text);
    public void Warn(string text) => Write("WARN", text);
    public void Error(string text, Exception? ex = null) => Write("ERROR", ex is null ? text : $"{text}: {ex.Message}");

    private void Write(string level, string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {text}";
        lock (_gate)
        {
            try { File.AppendAllText(_file, line + Environment.NewLine); } catch { }
        }
        Message?.Invoke(line);
    }
}
