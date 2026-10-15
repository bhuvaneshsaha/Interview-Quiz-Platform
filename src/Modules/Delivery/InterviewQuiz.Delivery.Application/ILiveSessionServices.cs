using InterviewQuiz.Delivery.Application.Contracts;

namespace InterviewQuiz.Delivery.Application;

/// <summary>
/// Evaluation reads session state and asks Delivery to end a session. It does not write the clock.
/// </summary>
public interface ILiveSessionGateway
{
    Task<LiveSessionView?> GetAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task TouchAsync(Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>Returns true when this call moved the session to ended because a pause cap was passed.</summary>
    Task<bool> TryEndForPauseLimitAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task MarkAutoEndedAsync(Guid assignmentId, string endReason, Guid? attemptId, CancellationToken cancellationToken);
}

public interface ILiveSessionCommands
{
    Task<LiveSessionView> GetRequiredAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task RecordLinkIssuedAsync(Guid assignmentId, string actorUserId, CancellationToken cancellationToken);

    Task MarkRunningAsync(Guid assignmentId, Guid attemptId, string actorUserId, CancellationToken cancellationToken);

    Task MarkPausedAsync(
        Guid assignmentId,
        Guid? attemptId,
        DateTimeOffset pausedAtUtc,
        string actorUserId,
        CancellationToken cancellationToken);

    Task MarkResumedAsync(
        Guid assignmentId,
        Guid? attemptId,
        int pausedSeconds,
        string actorUserId,
        CancellationToken cancellationToken);

    /// <summary>Returns false when the session was already terminal.</summary>
    Task<bool> MarkHostEndedAsync(
        Guid assignmentId,
        Guid? attemptId,
        string actorUserId,
        CancellationToken cancellationToken);

    Task SetPositionAsync(Guid assignmentId, int questionIndex, CancellationToken cancellationToken);
}
