using MewNX.Core;

namespace MewSwitchManager.Tests;

public sealed class TransactionalUpdateCoordinatorTests
{
    [Fact]
    public async Task RollsBackChangesAcrossMultipleManagedRoots()
    {
        var root = CreateTempDirectory();
        try
        {
            var first = Path.Combine(root, "first");
            var second = Path.Combine(root, "second");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            await File.WriteAllTextAsync(Path.Combine(first, "existing.txt"), "before");
            await File.WriteAllTextAsync(Path.Combine(second, "existing.txt"), "before-2");

            var coordinator = new TransactionalUpdateCoordinator(Path.Combine(root, "cache"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
                [first, second],
                async ct =>
                {
                    await File.WriteAllTextAsync(Path.Combine(first, "existing.txt"), "after", ct);
                    await File.WriteAllTextAsync(Path.Combine(first, "new.txt"), "new", ct);
                    await File.WriteAllTextAsync(Path.Combine(second, "existing.txt"), "after-2", ct);
                    await File.WriteAllTextAsync(Path.Combine(second, "new.txt"), "new-2", ct);
                    throw new InvalidOperationException("simulated failure");
                }));

            Assert.Equal("before", await File.ReadAllTextAsync(Path.Combine(first, "existing.txt")));
            Assert.Equal("before-2", await File.ReadAllTextAsync(Path.Combine(second, "existing.txt")));
            Assert.False(File.Exists(Path.Combine(first, "new.txt")));
            Assert.False(File.Exists(Path.Combine(second, "new.txt")));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task DoesNotLeaveNewRootAfterFailedTransaction()
    {
        var root = CreateTempDirectory();
        try
        {
            var missing = Path.Combine(root, "new-root");
            var coordinator = new TransactionalUpdateCoordinator(Path.Combine(root, "cache"));

            await Assert.ThrowsAsync<Exception>(() => coordinator.ExecuteAsync(
                [missing],
                _ =>
                {
                    Directory.CreateDirectory(missing);
                    File.WriteAllText(Path.Combine(missing, "payload.bin"), "payload");
                    return Task.FromException(new Exception("failure"));
                }));

            Assert.False(Directory.Exists(missing));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mewnx-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
