namespace InterviewQuiz.Delivery.Application;

public sealed record OpeningAssignmentItem(Guid AssignmentId, Guid QuizId, string CandidateEmail);

/// <summary>
/// Assignments for one opening. Callers do not read the delivery schema.
/// </summary>
public interface IOpeningAssignmentReader
{
    Task<IReadOnlyList<OpeningAssignmentItem>> ListByOpeningAsync(
        Guid openingId,
        CancellationToken cancellationToken);
}
