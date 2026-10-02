using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Delivery.Application;

public interface IAssignmentRepository
{
    Task<Assignment?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Assignment assignment, CancellationToken cancellationToken);

    Task<PagedResult<Assignment>> ListAsync(
        Guid? openingId,
        string? keyword,
        PageRequest page,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
