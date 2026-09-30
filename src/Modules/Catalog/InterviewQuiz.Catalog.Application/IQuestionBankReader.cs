using InterviewQuiz.Catalog.Application.Contracts;

namespace InterviewQuiz.Catalog.Application;

/// <summary>
/// Reserved for slice 4 bank include/validation. Slice 3 does not register an implementation
/// and must not call this from quiz CRUD, publish, or clone.
/// </summary>
public interface IQuestionBankReader
{
    Task<QuestionBankItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
