using MewNX.Core;
using MewNX.Models;
using Xunit;

namespace MewNX.Tests;

public sealed class RecoveryDecisionServiceTests
{
    private static readonly OperationJournalEntry Interrupted = new("op-1", "UsbWrite", "Running", DateTimeOffset.UtcNow);

    [Fact]
    public void UnknownIdentityRequiresManualIntervention()
    {
        var identity = new DiskIdentity("7", "", "", "", "", "", DiskIdentityConfidence.Unknown,
            DiskIdentitySourceStatus.QueryFailed);

        var decision = new RecoveryDecisionService().Decide(Interrupted, identity, physicalStateConsistent: true, safetyApproved: true);

        Assert.Equal(RecoveryDecision.ManualInterventionRequired, decision);
    }

    [Fact]
    public void UnexpectedPhysicalStateBlocksResume()
    {
        var identity = ConfirmedIdentity();

        var decision = new RecoveryDecisionService().Decide(Interrupted, identity, physicalStateConsistent: false, safetyApproved: true);

        Assert.Equal(RecoveryDecision.ManualInterventionRequired, decision);
    }

    [Fact]
    public void SafetyApprovalIsRequired()
    {
        var decision = new RecoveryDecisionService().Decide(Interrupted, ConfirmedIdentity(), true, safetyApproved: false);

        Assert.Equal(RecoveryDecision.ManualInterventionRequired, decision);
    }

    [Fact]
    public void OnlyFullyValidatedInterruptedOperationCanResume()
    {
        var decision = new RecoveryDecisionService().Decide(Interrupted, ConfirmedIdentity(), true, safetyApproved: true);

        Assert.Equal(RecoveryDecision.ResumeAllowed, decision);
    }

    private static DiskIdentity ConfirmedIdentity() =>
        new("7", "1234", "5678", "SERIAL-001", "USBSTOR\\example\\SERIAL-001", "ABC", DiskIdentityConfidence.Confirmed,
            DiskIdentitySourceStatus.Confirmed);
}
