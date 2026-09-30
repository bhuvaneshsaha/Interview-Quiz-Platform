using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Search.Application.Contracts;
using InterviewQuiz.Search.Domain;

namespace InterviewQuiz.Search.Application;

public interface IFilterRepository
{
    Task<SavedFilter?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(SavedFilter filter, CancellationToken cancellationToken);
    void Remove(SavedFilter filter);
    Task<PagedResult<SavedFilter>> ListVisibleAsync(
        string userId,
        FilterTarget? target,
        PageRequest page,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
