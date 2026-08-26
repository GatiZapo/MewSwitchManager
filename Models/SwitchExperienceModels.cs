using MewSwitchManager.Core;

namespace MewSwitchManager.Models;

public sealed record SwitchExperienceSummary(
    string Summary,
    IReadOnlyList<string> Healthy,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ToolRecommendation> Recommendations,
    SwitchSdReport Report,
    IReadOnlyList<SwitchToolHealth> Tools);
