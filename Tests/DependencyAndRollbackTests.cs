using MewSwitchManager.Core;
using MewSwitchManager.Models;

namespace MewSwitchManager.Tests;

public sealed class DependencyAndRollbackTests
{
    [Fact]
    public void SemVerOrdersPrereleaseBeforeRelease()
    {
        Assert.True(VersionConstraintParser.Compare("1.2.3-beta.2", "1.2.3") < 0);
        Assert.True(VersionConstraintParser.Compare("1.2.3-beta.2", "1.2.3-beta.10") < 0);
        Assert.Equal(0, VersionConstraintParser.Compare("v1.2.3+build.42", "1.2.3+other"));
    }

    [Fact]
    public void VersionConstraintHonoursInclusiveAndExclusiveBounds()
    {
        var inclusive = new VersionConstraint(Minimum: "1.0.0", Maximum: "2.0.0");
        var exclusive = new VersionConstraint(Minimum: "1.0.0", MinimumInclusive: false, Maximum: "2.0.0", MaximumInclusive: false);

        Assert.True(inclusive.Allows("1.0.0"));
        Assert.True(inclusive.Allows("2.0.0"));
        Assert.False(exclusive.Allows("1.0.0"));
        Assert.False(exclusive.Allows("2.0.0"));
        Assert.True(exclusive.Allows("1.5.0"));
    }

    [Fact]
    public void DependencyPlanOrdersDependenciesBeforeRequestedComponent()
    {
        var manifest = new[]
        {
            new ComponentManifestEntry("hekate", "Hekate", "stable", []),
            new ComponentManifestEntry("atmosphere", "Atmosphere", "stable", ["hekate"]),
            new ComponentManifestEntry("tesla", "Tesla", "stable", ["atmosphere"])
        };

        var plan = new DependencyManager().BuildPlan(manifest, ["tesla"], new Dictionary<string, string>());

        Assert.Equal(["hekate", "atmosphere", "tesla"], plan.InstallOrder);
        Assert.Empty(plan.Missing);
        Assert.Empty(plan.Cycles);
    }

    [Fact]
    public void DependencyPlanDetectsMissingDependenciesAndCycles()
    {
        var manifest = new[]
        {
            new ComponentManifestEntry("a", "A", "stable", ["missing"]),
            new ComponentManifestEntry("b", "B", "stable", ["c"]),
            new ComponentManifestEntry("c", "C", "stable", ["b"])
        };

        var plan = new DependencyManager().BuildPlan(manifest, ["a", "b"], new Dictionary<string, string>());

        Assert.Contains("missing", plan.Missing);
        Assert.Contains("b", plan.Cycles);
    }

    [Fact]
    public void TransactionRollbackRestoresExistingAndRemovesNewFiles()
    {
        var root = CreateTempDirectory();
        try
        {
            var component = Path.Combine(root, "component");
            Directory.CreateDirectory(component);
            File.WriteAllText(Path.Combine(component, "existing.txt"), "before");

            using (var transaction = new TransactionalRollback(root))
            {
                transaction.CaptureDirectory(component);
                File.WriteAllText(Path.Combine(component, "existing.txt"), "after");
                File.WriteAllText(Path.Combine(component, "new.txt"), "created");
                Directory.CreateDirectory(Path.Combine(component, "new-folder"));
                File.WriteAllText(Path.Combine(component, "new-folder", "nested.txt"), "nested");
            }

            Assert.Equal("before", File.ReadAllText(Path.Combine(component, "existing.txt")));
            Assert.False(File.Exists(Path.Combine(component, "new.txt")));
            Assert.False(Directory.Exists(Path.Combine(component, "new-folder")));
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void TransactionRollbackRemovesDirectoryCreatedAfterCapture()
    {
        var root = CreateTempDirectory();
        try
        {
            var component = Path.Combine(root, "new-component");
            using (var transaction = new TransactionalRollback(root))
            {
                transaction.CaptureDirectory(component);
                Directory.CreateDirectory(component);
                File.WriteAllText(Path.Combine(component, "created.txt"), "created");
            }

            Assert.False(Directory.Exists(component));
        }
        finally { TryDelete(root); }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MewNX-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
