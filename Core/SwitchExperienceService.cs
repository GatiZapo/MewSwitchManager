using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed class SwitchExperienceService
{
    private readonly SwitchSdInspector _inspector = new();
    private readonly SwitchToolValidator _validator = new();
    private readonly AppLogger _logger;

    public SwitchExperienceService(AppLogger logger) => _logger = logger;

    public SwitchExperienceSummary Inspect(string root)
    {
        var report = _inspector.Inspect(root);
        var tools = _validator.Validate(root);
        var healthy = new List<string>();
        var warnings = new List<string>(report.Warnings);

        if (report.HasHekate) healthy.Add("Hekate detected");
        if (report.HasAtmosphere) healthy.Add("Atmosphère detected");
        if (report.HasNintendo) healthy.Add("Nintendo content detected");
        if (report.HasEmummc) healthy.Add("emuMMC detected");
        if (report.HasBootloaderConfig) healthy.Add("Hekate configuration detected");

        var missingRecommended = tools.Where(x => !x.Installed && x.Definition.Id is "checkpoint" or "jksv").ToArray();
        var recommendations = new List<ToolRecommendation>();
        if (report.HasAtmosphere && missingRecommended.Any(x => x.Definition.Id == "checkpoint"))
            recommendations.Add(new("checkpoint", "Create save-data backups before major system changes.", true));
        if (report.HasAtmosphere && missingRecommended.Any(x => x.Definition.Id == "jksv"))
            recommendations.Add(new("jksv", "Useful second save-data management option.", true));
        if (!report.HasHekate)
            recommendations.Add(new("tegraexplorer", "A useful recovery/maintenance payload once RCM/Hekate is prepared.", false));
        if (report.HasAtmosphere && !tools.Any(x => x.Definition.Id == "status-monitor" && x.Installed))
            recommendations.Add(new("status-monitor", "Optional diagnostic overlay for performance and thermals.", false));

        if (report.FreeBytes < 512L * 1024 * 1024)
            warnings.Add("Less than 512 MB free space remains on the Switch storage.");
        if (!report.LooksLikeSwitchSd)
            warnings.Add("This drive does not look like a Switch SD card.");

        var summary = warnings.Count == 0
            ? "Switch storage looks healthy. No immediate action is required."
            : $"Switch storage scanned: {warnings.Count} item(s) need attention.";
        _logger.Info($"Switch experience scan completed for {root}: {summary}");
        return new SwitchExperienceSummary(summary, healthy, warnings, recommendations, report, tools);
    }
}
