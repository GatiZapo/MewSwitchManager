using MewSwitchManager.Core;

namespace MewSwitchManager.Tests;

public sealed class ComponentHealthServiceTests
{
    [Fact]
    public async Task DetectsMissingEmptyAndHealthyPayloads()
    {
        var root = Path.Combine(Path.GetTempPath(), "mewnx-health", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var atmosphere = Path.Combine(root, "atmosphere", "package3"); Directory.CreateDirectory(Path.GetDirectoryName(atmosphere)!); await File.WriteAllTextAsync(atmosphere, "valid");
            var dbi = Path.Combine(root, "switch", "DBI", "DBI.nro"); Directory.CreateDirectory(Path.GetDirectoryName(dbi)!); await File.WriteAllTextAsync(dbi, "");
            var report = await new ComponentHealthService().ScanAsync(root);
            var atmosphereResult = Assert.Single(report, x => x.ComponentId == "Atmosphere"); Assert.Equal(ComponentHealthSeverity.Healthy, atmosphereResult.Severity);
            var dbiResult = Assert.Single(report, x => x.ComponentId == "Dbi"); Assert.Equal(ComponentHealthSeverity.Broken, dbiResult.Severity);
            var hekateResult = Assert.Single(report, x => x.ComponentId == "Hekate"); Assert.Equal(ComponentHealthSeverity.Broken, hekateResult.Severity);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task DetectsHashMismatchWithoutModifyingFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "mewnx-health", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(root, "atmosphere")); var path = Path.Combine(root, "atmosphere", "package3"); await File.WriteAllTextAsync(path, "payload");
        try
        {
            var report = await new ComponentHealthService().ScanAsync(root, new Dictionary<string, string> { ["Atmosphere"] = "0000000000000000000000000000000000000000000000000000000000000000" });
            var result = Assert.Single(report, x => x.ComponentId == "Atmosphere"); Assert.Equal(ComponentHealthSeverity.Broken, result.Severity); Assert.Equal("payload", await File.ReadAllTextAsync(path));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
