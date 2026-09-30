using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Catalog.Application;

public interface IBankQuestionRepository
{
    Task<BankQuestion?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(BankQuestion question, CancellationToken cancellationToken);
    void SetExpectedRowVersion(BankQuestion question, uint expectedRowVersion);
    Task<PagedResult<BankQuestion>> ListAsync(
        BankQuestionListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
