using MewSwitchManager.Core;

namespace MewSwitchManager.Tests;

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
}
