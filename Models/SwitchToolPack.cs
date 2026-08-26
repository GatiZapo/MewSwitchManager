namespace MewSwitchManager.Models;

public sealed record SwitchToolPack(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> ToolIds)
{
    public override string ToString() => Name;
}
