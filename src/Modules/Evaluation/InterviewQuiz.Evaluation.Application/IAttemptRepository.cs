using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Evaluation.Application;

public interface IAttemptRepository
{
    Task<Attempt?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Attempt?> GetInProgressAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<Attempt?> GetLatestAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<int> CountSubmittedAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task AddAsync(Attempt attempt, CancellationToken cancellationToken);

    Task<PagedResult<Attempt>> ListByAssignmentAsync(
        Guid assignmentId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
