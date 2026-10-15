namespace InterviewQuiz.Evaluation.Application;

public sealed record AssignmentAttemptActivity(Guid AssignmentId, bool Started, bool Submitted);

/// <summary>
/// Attempt flags for assignment ids. Callers do not read the evaluation schema.
/// </summary>
public interface IAssignmentAttemptReader
{
    Task<IReadOnlyList<AssignmentAttemptActivity>> ListForAssignmentsAsync(
        IReadOnlyCollection<Guid> assignmentIds,
        CancellationToken cancellationToken);
}
