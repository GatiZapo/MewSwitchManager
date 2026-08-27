using MewSwitchManager.Core;

namespace MewSwitchManager.Tests;

public sealed class StateReconciliationTests
{
    [Fact]
    public void DetectsMissingAndEmptyFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "ok.bin"), "ok");
            File.WriteAllBytes(Path.Combine(root, "empty.bin"), []);
            var result = new StateReconciliationService().Reconcile(["ok.bin", "empty.bin", "missing.bin"], root);
            Assert.False(result.IsConsistent);
            Assert.Single(result.MissingPaths);
            Assert.Single(result.InvalidPaths);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DetectsHashMismatch()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "x.bin"), "actual");
            var result = await new StateReconciliationService().ReconcileHashesAsync(new Dictionary<string, string> { ["x.bin"] = "0000000000000000000000000000000000000000000000000000000000000000" }, root);
            Assert.False(result.IsConsistent);
            Assert.Single(result.InvalidPaths);
        }
        finally { Directory.Delete(root, true); }
    }
}
