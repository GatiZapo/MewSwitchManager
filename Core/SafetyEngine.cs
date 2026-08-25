using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed class SafetyEngine
{
    public bool IsSafeTarget(DiskInfo? disk) =>
        disk is not null &&
        disk.SafeCandidate &&
        !disk.Protected &&
        !disk.Boot &&
        !disk.System &&
        !disk.ReadOnly &&
        !disk.Offline &&
        disk.BusType.Equals("USB", StringComparison.OrdinalIgnoreCase);

    public string Explain(DiskInfo? disk)
    {
        if (disk is null) return "No target selected.";
        if (disk.Boot || disk.System) return "BLOCKED: Windows boot/system disk.";
        if (!disk.SafeCandidate) return $"BLOCKED: {disk.ProtectionReason}";
        if (disk.ReadOnly) return "BLOCKED: disk is read-only.";
        if (disk.Offline) return "BLOCKED: disk is offline.";
        if (!disk.BusType.Equals("USB", StringComparison.OrdinalIgnoreCase)) return "BLOCKED: only USB targets are allowed.";
        return "Safe USB candidate.";
    }

    public void DemandSafeTarget(DiskInfo? disk)
    {
        if (!IsSafeTarget(disk)) throw new InvalidOperationException(Explain(disk));
    }

    public void DemandStableIdentity(DiskInfo selected, DiskInfo? current)
    {
        DemandSafeTarget(current);
        if (current is null) throw new InvalidOperationException("TARGET MISSING: Windows no longer reports the selected USB disk. Operation aborted.");
        if (!string.Equals(selected.Number, current.Number, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("TARGET CHANGED: the Windows disk number changed. Operation aborted.");
        if (!string.IsNullOrWhiteSpace(selected.UniqueId) && !string.IsNullOrWhiteSpace(current.UniqueId) &&
            !string.Equals(selected.UniqueId, current.UniqueId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("TARGET CHANGED: the USB identity changed. Operation aborted.");
        if (Math.Abs(selected.SizeGb - current.SizeGb) > 0.5)
            throw new InvalidOperationException("TARGET CHANGED: the USB capacity changed. Operation aborted.");
    }
}
