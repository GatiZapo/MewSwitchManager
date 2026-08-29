using MewNX.Models;

namespace MewNX.Core;

public enum RecoveryDecision
{
    StopAndReconcile,
    ResumeAllowed,
    ManualInterventionRequired
}

/// <summary>Conservative recovery gate: journal history never authorizes destructive resume by itself.</summary>
public sealed class RecoveryDecisionService
{
    public RecoveryDecision Decide(OperationJournalEntry entry, DiskIdentity identity, bool physicalStateConsistent, bool safetyApproved)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(identity);

        if (string.Equals(entry.State, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.State, "RolledBack", StringComparison.OrdinalIgnoreCase))
            return RecoveryDecision.StopAndReconcile;

        if (identity.Confidence != DiskIdentityConfidence.Confirmed)
            return RecoveryDecision.ManualInterventionRequired;

        if (string.IsNullOrWhiteSpace(entry.TargetDiskFingerprint) ||
            !OperationJournal.TargetMatches(entry, identity))
            return RecoveryDecision.ManualInterventionRequired;

        if (!physicalStateConsistent)
            return RecoveryDecision.ManualInterventionRequired;

        if (!safetyApproved)
            return RecoveryDecision.ManualInterventionRequired;

        return RecoveryDecision.ResumeAllowed;
    }
}
