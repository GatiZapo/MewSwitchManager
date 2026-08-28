using System.Text.Json;
using MewNX.Models;

namespace MewNX.Core;

public sealed record OperationJournalEntry(
    string OperationId,
    string Kind,
    string State,
    DateTimeOffset Timestamp,
    string? Error = null,
    string? TargetDiskFingerprint = null,
    string? TargetDiskNumber = null);

public sealed class OperationJournal
{
    private readonly string _path;
    private readonly string _backupPath;
    private readonly string _temporaryPath;
    private readonly object _gate = new();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public OperationJournal(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Journal path is required.", nameof(path));

        _path = Path.GetFullPath(path);
        _backupPath = _path + ".bak";
        _temporaryPath = _path + ".tmp";
    }

    public void Append(OperationJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.OperationId))
            throw new ArgumentException("Operation ID is required.", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.TargetDiskFingerprint) &&
            string.Equals(entry.Kind, "UsbWrite", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Destructive storage operations require a target disk fingerprint.", nameof(entry));

        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var entries = LoadCore().ToList();
            entries.Add(entry);
            WriteAtomically(entries);
        }
    }

    public IReadOnlyList<OperationJournalEntry> Load()
    {
        lock (_gate) return LoadCore();
    }

    public IEnumerable<OperationJournalEntry> Incomplete()
        => Load()
            .GroupBy(entry => entry.OperationId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(entry => entry.Timestamp).First())
            .Where(entry =>
                !string.Equals(entry.State, "Completed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.State, "RolledBack", StringComparison.OrdinalIgnoreCase));

    public static bool TargetMatches(OperationJournalEntry entry, DiskIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(identity);
        return identity.Confidence == DiskIdentityConfidence.Confirmed &&
               !string.IsNullOrWhiteSpace(entry.TargetDiskFingerprint) &&
               string.Equals(entry.TargetDiskFingerprint, identity.CanonicalFingerprint, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<OperationJournalEntry> LoadCore()
    {
        if (TryRead(_path, out var entries)) return entries;
        if (TryRead(_backupPath, out entries)) return entries;
        return [];
    }

    private void WriteAtomically(IReadOnlyList<OperationJournalEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries, Options);
        using (var stream = new FileStream(_temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }
        if (File.Exists(_path)) File.Copy(_path, _backupPath, true);
        File.Move(_temporaryPath, _path, true);
    }

    private static bool TryRead(string path, out List<OperationJournalEntry> entries)
    {
        entries = [];
        if (!File.Exists(path)) return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<List<OperationJournalEntry>>(File.ReadAllText(path), Options);
            if (parsed is null) return false;
            entries = parsed;
            return true;
        }
        catch (JsonException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
