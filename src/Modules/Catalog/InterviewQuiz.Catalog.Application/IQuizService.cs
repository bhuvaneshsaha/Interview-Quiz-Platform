using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Catalog.Application.Contracts;

namespace InterviewQuiz.Catalog.Application;

public interface IQuizService
{
    Task<QuizResponse> CreateAsync(CreateQuizRequest request, CancellationToken cancellationToken);
    Task<QuizResponse> UpdateAsync(UpdateQuizRequest request, CancellationToken cancellationToken);
    Task<QuizResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<QuizResponse>> ListAsync(QuizListCriteria criteria, PageRequest page, CancellationToken cancellationToken);
}
