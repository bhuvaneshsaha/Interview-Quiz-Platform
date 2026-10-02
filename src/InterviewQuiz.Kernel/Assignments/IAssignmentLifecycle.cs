namespace InterviewQuiz.Kernel.Assignments;

/// <summary>
/// Delivery-owned status transitions. Evaluation must not write delivery tables.
/// </summary>
public interface IAssignmentLifecycle
{
    Task NotifyAttemptStarted(Guid assignmentId, Guid attemptId, CancellationToken cancellationToken);

    /// <param name="resultStatus"><c>complete</c> or <c>incomplete</c>.</param>
    Task NotifyAttemptSubmitted(
        Guid assignmentId,
        Guid attemptId,
        string resultStatus,
        CancellationToken cancellationToken);
}

public static class AttemptResultStatuses
{
    public const string Complete = "complete";
    public const string Incomplete = "incomplete";
}
