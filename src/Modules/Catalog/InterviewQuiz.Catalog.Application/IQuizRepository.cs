using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Catalog.Application;

public interface IQuizRepository
{
    Task<Quiz?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Quiz quiz, CancellationToken cancellationToken);
    void SetExpectedRowVersion(Quiz quiz, uint expectedRowVersion);
    Task<PagedResult<Quiz>> ListAsync(QuizListCriteria criteria, PageRequest page, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
