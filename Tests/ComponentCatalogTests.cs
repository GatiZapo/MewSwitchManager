using MewSwitchManager.Core;
using Xunit;

namespace MewSwitchManager.Tests;

public sealed class ComponentCatalogTests
{
    [Fact]
    public void CatalogLoadsManifestAndValidatesDependencies()
    {
        var root = Path.Combine(Path.GetTempPath(), "MewNX-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "manifest.json");
            File.WriteAllText(path, """
            {
              "schemaVersion": 2,
              "product": "MewNX",
              "components": [
                { "id": "a", "name": "A", "channel": "stable", "dependencies": [] },
                { "id": "b", "name": "B", "channel": "stable", "dependencies": ["a"] }
              ]
            }
            """);

            var snapshot = new ComponentCatalogService().Load(path);

            Assert.True(snapshot.IsValid, string.Join(" | ", snapshot.Errors));
            Assert.Equal(2, snapshot.Components.Count);
            Assert.Equal("a", snapshot.Components[1].Dependencies.Single());
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void CatalogRejectsUnknownDependency()
    {
        var root = Path.Combine(Path.GetTempPath(), "MewNX-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "manifest.json");
            File.WriteAllText(path, """
            {
              "schemaVersion": 2,
              "product": "MewNX",
              "components": [
                { "id": "tesla", "name": "Tesla", "channel": "stable", "dependencies": ["nx-ovlloader"] }
              ]
            }
            """);

            var snapshot = new ComponentCatalogService().Load(path);

            Assert.False(snapshot.IsValid);
            Assert.Contains(snapshot.Errors, x => x.Contains("nx-ovlloader", StringComparison.OrdinalIgnoreCase));
        }
        finally { TryDelete(root); }
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
