namespace MewSwitchManager.Models;

public sealed record DependencyInfo(string Name, string Command, bool Installed, bool Required, string Details);

public sealed record ComponentVersionInfo(string Id, string Version, string Channel = "stable");

public sealed record VersionConstraint(string? Minimum = null, bool MinimumInclusive = true, string? Maximum = null, bool MaximumInclusive = true, string? Exact = null)
{
    public bool Allows(string version) => VersionConstraintParser.Satisfies(version, this);
}
