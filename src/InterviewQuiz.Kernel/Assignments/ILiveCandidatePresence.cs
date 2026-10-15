namespace InterviewQuiz.Kernel.Assignments;

/// <summary>
/// Delivery-owned lobby presence. Access calls this after a live magic-link consume.
/// </summary>
public interface ILiveCandidatePresence
{
    Task RecordJoinedAsync(Guid assignmentId, CancellationToken cancellationToken);
}
