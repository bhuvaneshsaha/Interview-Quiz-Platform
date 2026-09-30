using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Search.Application.Contracts;
using InterviewQuiz.Search.Domain;

namespace InterviewQuiz.Search.Application;

public interface IFilterService
{
    Task<PagedResult<FilterResponse>> ListAsync(
        string userId,
        FilterTarget? target,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<FilterResponse> GetAsync(string userId, Guid id, CancellationToken cancellationToken);

    Task<FilterResponse> CreateAsync(string userId, CreateFilterRequest request, CancellationToken cancellationToken);

    Task<FilterResponse> UpdateAsync(
        string userId,
        Guid id,
        UpdateFilterRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(string userId, Guid id, CancellationToken cancellationToken);

    Task<FilterResponse> ShareAsync(
        string userId,
        Guid id,
        ShareFilterRequest request,
        CancellationToken cancellationToken);

    Task<FilterResponse> UnshareAsync(string userId, Guid id, CancellationToken cancellationToken);
}
