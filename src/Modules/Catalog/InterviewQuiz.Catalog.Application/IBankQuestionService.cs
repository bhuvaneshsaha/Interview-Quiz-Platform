using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Catalog.Application;

public interface IBankQuestionService
{
    Task<BankQuestionResponse> CreateAsync(CreateBankQuestionRequest request, CancellationToken cancellationToken);
    Task<BankQuestionResponse> UpdateAsync(Guid id, UpdateBankQuestionRequest request, CancellationToken cancellationToken);
    Task<BankQuestionResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<BankQuestionResponse>> ListAsync(
        BankQuestionListCriteria criteria,
        PageRequest page,
        bool canListArchived,
        CancellationToken cancellationToken);
    Task<BankQuestionResponse> ArchiveAsync(Guid id, CancellationToken cancellationToken);
    Task<BankQuestionResponse> UnarchiveAsync(Guid id, CancellationToken cancellationToken);
}
