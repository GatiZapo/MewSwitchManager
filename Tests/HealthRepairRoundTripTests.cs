using System.Security.Cryptography;
using MewNX.Core;
namespace MewSwitchManager.Tests;
public sealed class HealthRepairRoundTripTests
{
    [Fact]
    public async Task RepairRestoresIntegrityAndSecondCheckIsHealthy()
    {
        var root = Path.Combine(Path.GetTempPath(), "mewnx-repair", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var path = Path.Combine(root, "component.bin"); var source = Path.Combine(root, "source.bin");
        try
        {
            await File.WriteAllTextAsync(source, "known-good"); await File.WriteAllTextAsync(path, "corrupt");
            await using var stream = File.OpenRead(source); var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            var report = await new HealthRepairService().RepairAsync(root, new Dictionary<string,string> { ["component.bin"] = hash }, async (target, ct) => { await Task.Run(() => File.Copy(source, target, true), ct); return true; });
            Assert.True(report.Healthy); Assert.Equal("known-good", await File.ReadAllTextAsync(path));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
