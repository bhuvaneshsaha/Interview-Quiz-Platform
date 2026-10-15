namespace InterviewQuiz.Kernel.Assignments;

/// <summary>
/// Delivery-owned count of assignments that copied a quiz. Catalog reads this
/// so authors can see that edits do not reach existing snapshots.
/// </summary>
public interface IQuizAssignmentUsage
{
    Task<int> CountByQuizAsync(Guid quizId, CancellationToken cancellationToken);
}
