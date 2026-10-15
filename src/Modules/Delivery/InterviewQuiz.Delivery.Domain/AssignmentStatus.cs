namespace InterviewQuiz.Delivery.Domain;

/// <summary>
/// Assignment lifecycle. <see cref="PendingReview"/> and <see cref="Completed"/> are the only
/// terminal outcomes. There is no separate submitted state: submit moves straight to one of those.
/// </summary>
public enum AssignmentStatus
{
    NotStarted,
    InProgress,
    PendingReview,
    Completed
}
