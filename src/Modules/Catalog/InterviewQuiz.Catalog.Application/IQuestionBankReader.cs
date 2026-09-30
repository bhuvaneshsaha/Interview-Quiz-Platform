using InterviewQuiz.Catalog.Application.Contracts;

namespace InterviewQuiz.Catalog.Application;

/// <summary>
/// In-process bank lookup for copy-on-include. Quiz create/update/publish/clone must not call this.
/// GetById returns archived items so include can distinguish missing vs archived.
/// </summary>
public interface IQuestionBankReader
{
    Task<QuestionBankItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
