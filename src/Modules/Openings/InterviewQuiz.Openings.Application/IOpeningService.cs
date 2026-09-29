using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application.Contracts;

namespace InterviewQuiz.Openings.Application;

public interface IOpeningService
{
    Task<OpeningResponse> CreateAsync(CreateOpeningRequest request, CancellationToken cancellationToken);
    Task<OpeningResponse> UpdateAsync(UpdateOpeningRequest request, CancellationToken cancellationToken);
    Task<OpeningResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<OpeningResponse>> ListAsync(OpeningListCriteria criteria, PageRequest page, CancellationToken cancellationToken);
}
