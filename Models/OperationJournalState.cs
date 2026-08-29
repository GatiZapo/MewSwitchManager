namespace MewNX.Models;

public enum OperationJournalState
{
    Prepared,
    Writing,
    Verifying,
    Committed,
    RolledBack,
    Interrupted
}

public static class OperationJournalTransitions
{
    public static bool IsValid(OperationJournalState from, OperationJournalState to)
        => (from, to) switch
        {
            (OperationJournalState.Prepared, OperationJournalState.Writing) => true,
            (OperationJournalState.Writing, OperationJournalState.Verifying) => true,
            (OperationJournalState.Writing, OperationJournalState.Interrupted) => true,
            (OperationJournalState.Verifying, OperationJournalState.Committed) => true,
            (OperationJournalState.Verifying, OperationJournalState.Interrupted) => true,
            (OperationJournalState.Interrupted, OperationJournalState.RolledBack) => true,
            _ => false
        };
}
