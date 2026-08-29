using MewNX.Models;
using Xunit;

namespace MewNX.Tests;

public sealed class OperationJournalStateTests
{
    [Theory]
    [InlineData(OperationJournalState.Prepared, OperationJournalState.Writing, true)]
    [InlineData(OperationJournalState.Writing, OperationJournalState.Verifying, true)]
    [InlineData(OperationJournalState.Writing, OperationJournalState.Interrupted, true)]
    [InlineData(OperationJournalState.Verifying, OperationJournalState.Committed, true)]
    [InlineData(OperationJournalState.Verifying, OperationJournalState.Interrupted, true)]
    [InlineData(OperationJournalState.Interrupted, OperationJournalState.RolledBack, true)]
    [InlineData(OperationJournalState.Prepared, OperationJournalState.Committed, false)]
    [InlineData(OperationJournalState.Writing, OperationJournalState.Committed, false)]
    [InlineData(OperationJournalState.Committed, OperationJournalState.Writing, false)]
    [InlineData(OperationJournalState.RolledBack, OperationJournalState.Writing, false)]
    public void TransitionGraphIsExplicit(OperationJournalState from, OperationJournalState to, bool expected)
    {
        Assert.Equal(expected, OperationJournalTransitions.IsValid(from, to));
    }
}
