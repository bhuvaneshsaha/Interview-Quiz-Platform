using InterviewQuiz.Evaluation.Application.Contracts;

namespace InterviewQuiz.Evaluation.Application;

/// <summary>
/// Evaluation-owned attempt clock. Delivery session state calls this in-process.
/// </summary>
public interface IAttemptClock
{
    Task ApplyLazyAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<Guid> StartForHostAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<DateTimeOffset> PauseAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<int> ResumeAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task ForceSubmitAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<AttemptLiveFacts?> GetFactsAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task SetFlagsAsync(
        Guid assignmentId,
        IReadOnlyList<ReviewFlagUpdate> flags,
        CancellationToken cancellationToken);
}
