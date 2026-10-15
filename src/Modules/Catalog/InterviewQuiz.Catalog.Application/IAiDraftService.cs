using InterviewQuiz.Catalog.Application.Contracts;

namespace InterviewQuiz.Catalog.Application;

public interface IAiDraftService
{
    Task<AiDraftResponse> CreateAsync(
        CreateAiDraftRequest request,
        string userId,
        CancellationToken cancellationToken);

    Task<AiDraftResponse> GetAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<AiDraftAcceptResponse> AcceptAsync(
        Guid id,
        AcceptAiDraftRequest request,
        string userId,
        CancellationToken cancellationToken);
}
