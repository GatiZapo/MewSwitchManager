using MewNX.Core;
using MewNX.Models;
using Xunit;

namespace MewNX.Tests;

public sealed class RecoveryDecisionServiceTests
{
    [Fact]
    public void UnknownIdentityRequiresManualIntervention()
    {
        var decision = Decide(Entry(), new DiskIdentity("7", "", "", "", "", "", DiskIdentityConfidence.Unknown,
            DiskIdentitySourceStatus.QueryFailed), true, true);

        Assert.Equal(RecoveryDecision.ManualInterventionRequired, decision);
    }

    [Fact]
    public void MissingJournalFingerprintRequiresManualIntervention()
    {
        var decision = Decide(Entry(null), ConfirmedIdentity(), true, true);
        Assert.Equal(RecoveryDecision.ManualInterventionRequired, decision);
    }

    [Fact]
    public void MismatchedJournalFingerprintRequiresManualIntervention()
    {
        var decision = Decide(Entry("OTHER"), ConfirmedIdentity(), true, true);
        Assert.Equal(RecoveryDecision.ManualInterventionRequired, decision);
    }

    [Fact]
    public void UnexpectedPhysicalStateBlocksResume()
    {
        var decision = Decide(Entry(), ConfirmedIdentity(), false, true);
        Assert.Equal(RecoveryDecision.ManualInterventionRequired, decision);
    }

    [Fact]
    public void SafetyApprovalIsRequired()
    {
        var decision = Decide(Entry(), ConfirmedIdentity(), true, false);
        Assert.Equal(RecoveryDecision.ManualInterventionRequired, decision);
    }

    [Fact]
    public void OnlyFullyValidatedInterruptedOperationCanResume()
    {
        var decision = Decide(Entry(), ConfirmedIdentity(), true, true);
        Assert.Equal(RecoveryDecision.ResumeAllowed, decision);
    }

    private static RecoveryDecision Decide(OperationJournalEntry entry, DiskIdentity identity, bool physicalStateConsistent, bool safetyApproved) =>
        new RecoveryDecisionService().Decide(entry, identity, physicalStateConsistent, safetyApproved);

    private static OperationJournalEntry Entry(string? fingerprint = "ABC") =>
        new("op-1", "UsbWrite", "Writing", DateTimeOffset.UtcNow, null, fingerprint, "7");

    private static DiskIdentity ConfirmedIdentity() =>
        new("7", "1234", "5678", "SERIAL-001", "USBSTOR\\example\\SERIAL-001", "ABC", DiskIdentityConfidence.Confirmed,
            DiskIdentitySourceStatus.Confirmed);
}
