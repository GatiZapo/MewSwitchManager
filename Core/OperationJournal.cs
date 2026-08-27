using System.Text.Json;

namespace MewSwitchManager.Core;

public sealed record OperationJournalEntry(string OperationId, string Kind, string State, DateTimeOffset Timestamp, string? Error = null);

public sealed class OperationJournal
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public OperationJournal(string path) => _path = path;

    public void Append(OperationJournalEntry entry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var entries = Load().ToList();
        entries.Add(entry);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(entries, Options));
        File.Move(tmp, _path, true);
    }

    public IReadOnlyList<OperationJournalEntry> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<OperationJournalEntry>>(File.ReadAllText(_path), Options) ?? [] : []; }
        catch { return []; }
    }

    public IEnumerable<OperationJournalEntry> Incomplete()
        => Load().GroupBy(x => x.OperationId).Select(g => g.OrderByDescending(x => x.Timestamp).First())
            .Where(x => !string.Equals(x.State, "Completed", StringComparison.OrdinalIgnoreCase) && !string.Equals(x.State, "RolledBack", StringComparison.OrdinalIgnoreCase));
}
