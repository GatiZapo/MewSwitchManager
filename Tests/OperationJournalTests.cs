using MewNX.Core;
using MewNX.Models;

namespace MewNX.Tests;

public sealed class OperationJournalTests
{
    [Fact]
    public void FindsLatestIncompleteOperation()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var journal = new OperationJournal(Path.Combine(root, "journal.json"));
            journal.Append(new("op", "update", "Started", DateTimeOffset.UtcNow));
            journal.Append(new("op", "update", "Staging", DateTimeOffset.UtcNow.AddSeconds(1)));
            Assert.Single(journal.Incomplete());
            Assert.Equal("Staging", journal.Incomplete().Single().State);
            journal.Append(new("op", "update", "Completed", DateTimeOffset.UtcNow.AddSeconds(2)));
            Assert.Empty(journal.Incomplete());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void RecoversFromCorruptedPrimaryUsingBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "journal.json");
            var journal = new OperationJournal(path);
            journal.Append(new("op", "update", "Started", DateTimeOffset.UtcNow));
            journal.Append(new("op", "update", "Staging", DateTimeOffset.UtcNow.AddSeconds(1)));
            File.WriteAllText(path, "{ truncated");
            var recovered = journal.Load();
            Assert.Single(recovered);
            Assert.Equal("Started", recovered[0].State);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void UsbWriteRequiresTargetFingerprint()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var journal = new OperationJournal(Path.Combine(root, "journal.json"));
            Assert.Throws<ArgumentException>(() => journal.Append(new("op", "UsbWrite", "Running", DateTimeOffset.UtcNow)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void TargetFingerprintMustMatchConfirmedIdentity()
    {
        var entry = new OperationJournalEntry("op", "UsbWrite", "Running", DateTimeOffset.UtcNow, TargetDiskFingerprint: "ABC");
        var matching = new DiskIdentity("7", "1234", "5678", "SERIAL", "USB\\VID_1234&PID_5678\\SERIAL", "ABC", DiskIdentityConfidence.Confirmed, DiskIdentitySourceStatus.Confirmed);
        var different = matching with { CanonicalFingerprint = "DEF" };
        var unknown = matching with { Confidence = DiskIdentityConfidence.Unknown };

        Assert.True(OperationJournal.TargetMatches(entry, matching));
        Assert.False(OperationJournal.TargetMatches(entry, different));
        Assert.False(OperationJournal.TargetMatches(entry, unknown));
    }

    [Fact]
    public void InterruptedUsbWriteRemainsIncompleteUntilExplicitlyCommitted()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var journal = new OperationJournal(Path.Combine(root, "journal.json"));
            journal.Append(new("op", "UsbWrite", "Prepared", DateTimeOffset.UtcNow, TargetDiskFingerprint: "ABC"));
            journal.Append(new("op", "UsbWrite", "Writing", DateTimeOffset.UtcNow.AddSeconds(1), TargetDiskFingerprint: "ABC"));

            var incomplete = journal.Incomplete().Single();
            Assert.Equal("Writing", incomplete.State);
            Assert.NotEqual("Completed", incomplete.State);
            Assert.NotEqual("Committed", incomplete.State);
        }
        finally { Directory.Delete(root, true); }
    }
}
