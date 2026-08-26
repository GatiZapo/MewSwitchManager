using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed class SwitchToolManager
{
    private readonly GitHubReleaseClient _releases;
    private readonly AppLogger _logger;

    public SwitchToolManager(AppLogger logger)
    {
        _logger = logger;
        _releases = new GitHubReleaseClient(logger);
    }

    public IReadOnlyList<SwitchToolDefinition> Catalog => SwitchToolCatalog.Definitions;

    public async Task<IReadOnlyList<SwitchToolStatus>> ScanAsync(string targetRoot, CancellationToken ct = default)
    {
        if (!Directory.Exists(targetRoot)) throw new DirectoryNotFoundException(targetRoot);
        var result = new List<SwitchToolStatus>();
        foreach (var definition in Catalog)
        {
            ct.ThrowIfCancellationRequested();
            var installed = File.Exists(Path.Combine(targetRoot, definition.Destination));
            try
            {
                var release = await _releases.GetLatestAsync(definition.Repository, ct);
                result.Add(new SwitchToolStatus(definition, installed, installed ? "Detected" : "Not installed", release.TagName, false, release.HtmlUrl, release.Name));
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not query {definition.Name}: {ex.Message}");
                result.Add(new SwitchToolStatus(definition, installed, installed ? "Detected" : "Not installed", "Unavailable", false, null, "Release check failed; no files were changed."));
            }
        }
        return result;
    }
}
