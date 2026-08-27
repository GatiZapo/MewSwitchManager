using MewSwitchManager.Core;
using MewSwitchManager.Models;
namespace MewSwitchManager.Tests;
public sealed class SafetyEngineTests
{
    [Fact]
    public void RejectsNonUsbSystemDisk()
    {
        var disk = new DiskInfo("0", "Windows", 512, "Fixed", false, true, true, false, false, "System disk");
        var safety = new SafetyEngine();
        Assert.False(safety.IsSafeTarget(disk));
        Assert.Contains("system", safety.Explain(disk), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsIdentityChange()
    {
        var selected = new DiskInfo("2", "Switch SD", 64, "USB", true, false, false, false, false, "", "A");
        var current = selected with { UniqueId = "B" };
        Assert.Throws<InvalidOperationException>(() => new SafetyEngine().DemandStableIdentity(selected, current));
    }
}
