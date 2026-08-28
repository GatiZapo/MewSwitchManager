using MewNX.Core;

namespace MewSwitchManager.Tests;

public sealed class TransactionalUpdateServiceTests
{
    [Fact]
    public async Task FailedVerificationRollsBackAndJournals()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "component.bin");
        File.WriteAllText(target, "before");
        try
        {
            var journal = new OperationJournal(Path.Combine(root, "journal.json"));
            var service = new TransactionalUpdateService(journal);
            var result = await service.ExecuteAsync("op", [target], root,
                _ => { File.WriteAllText(target, "after"); return Task.CompletedTask; },
                _ => Task.FromResult(false));
            Assert.False(result.Success);
            Assert.True(result.RolledBack);
            Assert.True(result.RollbackVerified);
            Assert.Equal("before", File.ReadAllText(target));
            Assert.Empty(journal.Incomplete());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SuccessfulUpdateCommits()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "component.bin");
        File.WriteAllText(target, "before");
        try
        {
            var journal = new OperationJournal(Path.Combine(root, "journal.json"));
            var service = new TransactionalUpdateService(journal);
            var result = await service.ExecuteAsync("op", [target], root,
                _ => { File.WriteAllText(target, "after"); return Task.CompletedTask; },
                _ => Task.FromResult(true));
            Assert.True(result.Success);
            Assert.False(result.RolledBack);
            Assert.Equal("after", File.ReadAllText(target));
            Assert.Empty(journal.Incomplete());
        }
        finally { Directory.Delete(root, true); }
    }
}
