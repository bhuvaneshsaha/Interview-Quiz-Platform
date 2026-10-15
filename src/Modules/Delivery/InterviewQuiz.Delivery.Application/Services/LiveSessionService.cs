using System.Diagnostics;
using System.Text.Json;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Delivery.Application.Services;

public sealed class LiveSessionService : ILiveCandidatePresence, ILiveSessionGateway, ILiveSessionCommands
{
    private readonly IAssignmentRepository _assignments;
    private readonly IClock _clock;
    private readonly ICorrelationAccessor _correlation;
    private readonly ILogger<LiveSessionService> _logger;

    public LiveSessionService(
        IAssignmentRepository assignments,
        IClock clock,
        ICorrelationAccessor correlation,
        ILogger<LiveSessionService> logger)
    {
        _assignments = assignments;
        _clock = clock;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task RecordJoinedAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken);
        if (assignment is null || assignment.Mode != AssignmentMode.Live)
        {
            return;
        }

        var first = assignment.NoteCandidateJoined(_clock);
        if (first)
        {
            Audit(assignment.Id, LiveSessionActionNames.CandidateJoined, null, null, null);
            Log(LiveSessionActionNames.CandidateJoined, assignment.Id, null, null);
        }

        await _assignments.SaveChangesAsync(cancellationToken);
    }

    public async Task<LiveSessionView?> GetAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken);
        return assignment is null ? null : ToView(assignment);
    }

    public async Task TouchAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken);
        if (assignment is null || assignment.Mode != AssignmentMode.Live)
        {
            return;
        }

        assignment.NoteCandidateSeen(_clock);
        await _assignments.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryEndForPauseLimitAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken);
        if (assignment is null || !assignment.PauseLimitExceeded(_clock))
        {
            return false;
        }

        if (!assignment.MarkLiveEnded(LiveEndReason.PauseLimit, _clock))
        {
            return false;
        }

        Audit(assignment.Id, LiveSessionActionNames.AutoEnded, null, null, "endReason=pauseLimit");
        Log(LiveSessionActionNames.AutoEnded, assignment.Id, null, null);
        await _assignments.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkAutoEndedAsync(
        Guid assignmentId,
        string endReason,
        Guid? attemptId,
        CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken);
        if (assignment is null || assignment.Mode != AssignmentMode.Live)
        {
            return;
        }

        var reason = ParseReason(endReason);
        if (!assignment.MarkLiveEnded(reason, _clock))
        {
            return;
        }

        Audit(assignment.Id, LiveSessionActionNames.AutoEnded, attemptId, null, $"endReason={endReason}");
        Log(LiveSessionActionNames.AutoEnded, assignment.Id, attemptId, null);
        await _assignments.SaveChangesAsync(cancellationToken);
    }

    public async Task<LiveSessionView> GetRequiredAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await RequireAsync(assignmentId, cancellationToken);
        return ToView(assignment);
    }

    public async Task RecordLinkIssuedAsync(
        Guid assignmentId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var assignment = await RequireAsync(assignmentId, cancellationToken);
        if (assignment.Mode != AssignmentMode.Live)
        {
            return;
        }

        Audit(assignment.Id, LiveSessionActionNames.LinkIssued, null, actorUserId, null);
        Log(LiveSessionActionNames.LinkIssued, assignment.Id, null, actorUserId);
        await _assignments.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkRunningAsync(
        Guid assignmentId,
        Guid attemptId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var assignment = await RequireAsync(assignmentId, cancellationToken);
        assignment.MarkLiveRunning(_clock);
        Audit(assignment.Id, LiveSessionActionNames.Started, attemptId, actorUserId, null);
        Log(LiveSessionActionNames.Started, assignment.Id, attemptId, actorUserId);
        await _assignments.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPausedAsync(
        Guid assignmentId,
        Guid? attemptId,
        DateTimeOffset pausedAtUtc,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var assignment = await RequireAsync(assignmentId, cancellationToken);
        assignment.MarkLivePaused(pausedAtUtc, _clock);
        Audit(assignment.Id, LiveSessionActionNames.Paused, attemptId, actorUserId, null);
        Log(LiveSessionActionNames.Paused, assignment.Id, attemptId, actorUserId);
        await _assignments.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkResumedAsync(
        Guid assignmentId,
        Guid? attemptId,
        int pausedSeconds,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var assignment = await RequireAsync(assignmentId, cancellationToken);
        assignment.MarkLiveResumed(pausedSeconds, _clock);
        Audit(
            assignment.Id,
            LiveSessionActionNames.Resumed,
            attemptId,
            actorUserId,
            $"pausedSeconds={pausedSeconds}");
        Log(LiveSessionActionNames.Resumed, assignment.Id, attemptId, actorUserId);
        await _assignments.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> MarkHostEndedAsync(
        Guid assignmentId,
        Guid? attemptId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var assignment = await RequireAsync(assignmentId, cancellationToken);
        if (!assignment.MarkLiveEnded(LiveEndReason.Host, _clock))
        {
            return false;
        }

        Audit(assignment.Id, LiveSessionActionNames.Ended, attemptId, actorUserId, "endReason=host");
        Log(LiveSessionActionNames.Ended, assignment.Id, attemptId, actorUserId);
        await _assignments.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetPositionAsync(Guid assignmentId, int questionIndex, CancellationToken cancellationToken)
    {
        var assignment = await RequireAsync(assignmentId, cancellationToken);
        assignment.SetCurrentQuestion(questionIndex, _clock);
        await _assignments.SaveChangesAsync(cancellationToken);
    }

    private async Task<Assignment> RequireAsync(Guid assignmentId, CancellationToken cancellationToken)
        => await _assignments.GetAsync(assignmentId, cancellationToken)
           ?? throw new EntityNotFoundException(nameof(Assignment), assignmentId);

    private void Audit(Guid assignmentId, string action, Guid? attemptId, string? actorUserId, string? detail)
    {
        _assignments.AddLiveEvent(LiveSessionEvent.Create(
            assignmentId,
            action,
            _clock.UtcNow,
            _correlation.Current,
            attemptId,
            actorUserId,
            detail));
    }

    private void Log(string action, Guid assignmentId, Guid? attemptId, string? actorUserId)
    {
        using var activity = AssignmentService.ActivitySource.StartActivity($"live.{ActivityName(action)}");
        activity?.SetTag("assignment.id", assignmentId);
        if (attemptId is { } id)
        {
            activity?.SetTag("attempt.id", id);
        }

        _logger.LogInformation(
            "Live session {Action} assignment {AssignmentId} attempt {AttemptId} actor {ActorUserId}",
            action,
            assignmentId,
            attemptId,
            actorUserId);
    }

    private static string ActivityName(string action)
        => action switch
        {
            LiveSessionActionNames.Started => "start",
            LiveSessionActionNames.Paused => "pause",
            LiveSessionActionNames.Resumed => "resume",
            LiveSessionActionNames.Ended => "end",
            LiveSessionActionNames.AutoEnded => "end",
            _ => "progress"
        };

    private static LiveEndReason ParseReason(string endReason)
        => endReason switch
        {
            LiveEndReasons.Timeout => LiveEndReason.Timeout,
            LiveEndReasons.PauseLimit => LiveEndReason.PauseLimit,
            LiveEndReasons.Submitted => LiveEndReason.Submitted,
            _ => LiveEndReason.Host
        };

    internal static LiveSessionView ToView(Assignment assignment)
        => new(
            assignment.Id,
            Camel(assignment.Mode),
            Camel(assignment.Status),
            assignment.OverallDurationMinutes,
            Camel(assignment.EffectiveLiveState),
            assignment.LiveStartedAtUtc,
            assignment.PausedAtUtc,
            assignment.TotalPausedSeconds,
            assignment.LiveEndedAtUtc,
            assignment.SessionEndReason is { } reason ? Camel(reason) : null,
            assignment.CandidateLastSeenAtUtc,
            assignment.CurrentQuestionIndex,
            assignment.LastActivityAtUtc,
            assignment.Snapshot?.QuestionCount ?? 0);

    private static string Camel(Enum value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
