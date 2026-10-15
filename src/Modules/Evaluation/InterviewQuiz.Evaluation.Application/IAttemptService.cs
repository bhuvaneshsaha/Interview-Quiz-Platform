using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Evaluation.Application;

public interface IAttemptService
{
    Task<(CandidateAttemptResponse Response, bool Created)> StartAsync(
        Guid assignmentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Welcome facts and attempt phase. Does not create an attempt and does not set <c>dueAtUtc</c>.
    /// </summary>
    Task<CandidateAttemptPreviewResponse> GetPreviewAsync(
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<CandidateAttemptResponse> GetCurrentAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<CandidateAttemptResponse> SaveAnswersAsync(
        Guid assignmentId,
        SaveAnswersRequest request,
        CancellationToken cancellationToken);

    Task<CandidateSubmitResponse> SubmitAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<PagedResult<AttemptSummaryResponse>> ListByAssignmentAsync(
        Guid assignmentId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<AttemptResultResponse> GetResultAsync(Guid attemptId, CancellationToken cancellationToken);

    Task<AttemptReviewResponse> GetReviewAsync(Guid attemptId, CancellationToken cancellationToken);

    Task<AttemptReviewResponse> ScoreItemAsync(
        Guid attemptId,
        Guid questionId,
        ReviewItemRequest request,
        string reviewerUserId,
        CancellationToken cancellationToken);

    Task<AttemptReviewResponse> SuggestItemAsync(
        Guid attemptId,
        Guid questionId,
        string reviewerUserId,
        CancellationToken cancellationToken);
}
