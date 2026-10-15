using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Application;

public interface IAiDraftRepository
{
    Task AddAsync(AiDraft draft, CancellationToken cancellationToken);

    Task<AiDraft?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
