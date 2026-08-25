namespace MewSwitchManager.Models;

public sealed record DiskInfo(
    string Number,
    string Model,
    double SizeGb,
    string BusType,
    bool Boot,
    bool System,
    bool ReadOnly,
    bool Offline,
    bool SafeCandidate,
    string ProtectionReason = "",
    string UniqueId = "")
{
    public bool Protected => !SafeCandidate || Boot || System || ReadOnly || Offline;
    public string DisplayName => $"Disk {Number} · {Model} · {SizeGb:0.0} GB · {BusType}";
    public override string ToString() => DisplayName;
}
