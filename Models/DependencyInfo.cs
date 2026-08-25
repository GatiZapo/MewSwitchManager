namespace MewSwitchManager.Models;

public sealed record DependencyInfo(string Name, string Command, bool Installed, bool Required, string Details);
