using MewSwitchManager.Core;
namespace MewSwitchManager.Tests;
public sealed class HealthRepairServiceTests
{
    [Fact]
    public async Task ReportsMissingAndInvalidFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var existing = Path.Combine(root, "ok.bin"); File.WriteAllText(existing, "ok");
            var expected = new Dictionary<string,string> { ["ok.bin"] = "00", ["missing.bin"] = "00" };
            var report = await new HealthRepairService().CheckAsync(root, expected);
            Assert.False(report.Healthy); Assert.Equal(2, report.Issues.Count);
        }
        finally { Directory.Delete(root, true); }
    }
}
