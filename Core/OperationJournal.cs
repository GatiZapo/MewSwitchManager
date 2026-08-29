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
        ValidateEntry(entry);

        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var entries = LoadCore().ToList();
            ValidateTransition(entries, entry);
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
                !string.Equals(entry.State, nameof(OperationJournalState.Committed), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.State, nameof(OperationJournalState.RolledBack), StringComparison.OrdinalIgnoreCase));

    public static bool TargetMatches(OperationJournalEntry entry, DiskIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(identity);
        return identity.Confidence == DiskIdentityConfidence.Confirmed &&
               !string.IsNullOrWhiteSpace(entry.TargetDiskFingerprint) &&
               string.Equals(entry.TargetDiskFingerprint, identity.CanonicalFingerprint, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateEntry(OperationJournalEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.OperationId))
            throw new ArgumentException("Operation ID is required.", nameof(entry));
        if (!Enum.TryParse<OperationJournalState>(entry.State, true, out _))
            throw new ArgumentException("Unknown journal state.", nameof(entry));
        if (string.Equals(entry.Kind, "UsbWrite", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(entry.TargetDiskFingerprint))
            throw new ArgumentException("Destructive storage operations require a target disk fingerprint.", nameof(entry));
    }

    private static void ValidateTransition(IReadOnlyList<OperationJournalEntry> entries, OperationJournalEntry next)
    {
        var previous = entries
            .Where(entry => string.Equals(entry.OperationId, next.OperationId, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.Timestamp)
            .FirstOrDefault();

        if (previous is null)
        {
            if (!Enum.TryParse<OperationJournalState>(next.State, true, out var initial) || initial != OperationJournalState.Prepared)
                throw new InvalidOperationException("A new operation must start in Prepared state.");
            return;
        }

        if (!Enum.TryParse<OperationJournalState>(previous.State, true, out var from) ||
            !Enum.TryParse<OperationJournalState>(next.State, true, out var to) ||
            !OperationJournalTransitions.IsValid(from, to))
            throw new InvalidOperationException($"Invalid journal transition: {previous.State} -> {next.State}.");
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
