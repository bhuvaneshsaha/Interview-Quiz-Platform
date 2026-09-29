using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Domain;

namespace InterviewQuiz.Openings.Application;

public interface IOpeningRepository
{
    Task<Opening?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Opening opening, CancellationToken cancellationToken);
    void SetExpectedRowVersion(Opening opening, uint expectedRowVersion);
    Task<PagedResult<Opening>> ListAsync(OpeningListCriteria criteria, PageRequest page, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
