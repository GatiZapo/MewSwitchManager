using MewSwitchManager.Core;
namespace MewSwitchManager.Tests;
public sealed class UpdatePlanTests
{
    [Fact]
    public void RejectsDuplicateComponentsAndInvalidDependencies()
    {
        var plan = new UpdatePlanBuilder().Build([
            new("atmosphere", "1", "2", ["hekate"]),
            new("atmosphere", "1", "3", [""])
        ]);
        Assert.False(plan.IsValid);
        Assert.Contains(plan.Errors, e => e.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Errors, e => e.Contains("Invalid dependency", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AcceptsValidPlan()
    {
        var plan = new UpdatePlanBuilder().Build([new("hekate", "6", "7", [])]);
        Assert.True(plan.IsValid);
        Assert.Single(plan.Items);
    }
}
