namespace InterviewQuiz.Kernel.Assignments;

/// <summary>
/// Evaluation-owned lookup so Access can put <c>attempt_id</c> on a candidate JWT after start.
/// Optional until Evaluation registers an implementation; omit the claim when null.
/// </summary>
public interface ICandidateAttemptIdLookup
{
    Task<Guid?> GetAttemptIdAsync(Guid assignmentId, CancellationToken cancellationToken);
}
