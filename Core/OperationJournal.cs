using System.Text.Json;

namespace MewNX.Core;

public sealed record OperationJournalEntry(
    string OperationId,
    string Kind,
    string State,
    DateTimeOffset Timestamp,
    string? Error = null);

public sealed class OperationJournal
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public OperationJournal(string path)
        => _path = path;

    public void Append(OperationJournalEntry entry)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var entries = Load().ToList();
        entries.Add(entry);

        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries, Options));
        File.Move(temporaryPath, _path, true);
    }

    public IReadOnlyList<OperationJournalEntry> Load()
    {
        if (!File.Exists(_path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<OperationJournalEntry>>(
                       File.ReadAllText(_path), Options)
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public IEnumerable<OperationJournalEntry> Incomplete()
        => Load()
            .GroupBy(entry => entry.OperationId)
            .Select(group => group.OrderByDescending(entry => entry.Timestamp).First())
            .Where(entry =>
                !string.Equals(entry.State, "Completed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.State, "RolledBack", StringComparison.OrdinalIgnoreCase));
}
