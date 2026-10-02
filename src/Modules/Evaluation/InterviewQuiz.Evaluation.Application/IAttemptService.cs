using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Evaluation.Application;

public interface IAttemptService
{
    Task<(CandidateAttemptResponse Response, bool Created)> StartAsync(
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
}
