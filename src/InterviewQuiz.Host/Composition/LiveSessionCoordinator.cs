using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Delivery.Application.Services;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Host.Composition;

public sealed class LiveSessionCoordinator
{
    private readonly ILiveSessionCommands _sessions;
    private readonly IAttemptClock _clockPort;
    private readonly IClock _clock;
    private readonly ILogger<LiveSessionCoordinator> _logger;

    public LiveSessionCoordinator(
        ILiveSessionCommands sessions,
        IAttemptClock clockPort,
        IClock clock,
        ILogger<LiveSessionCoordinator> logger)
    {
        _sessions = sessions;
        _clockPort = clockPort;
        _clock = clock;
        _logger = logger;
    }

    public async Task<LiveProgressResponse> StartAsync(
        Guid assignmentId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        using var activity = AssignmentService.ActivitySource.StartActivity("live.start");
        activity?.SetTag("assignment.id", assignmentId);
        var view = await _sessions.GetRequiredAsync(assignmentId, cancellationToken);
        if (!string.Equals(view.Mode, "live", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(LiveSessionMessages.NotLive);
        }

        if (view.OverallDurationMinutes is null)
        {
            throw new DomainException(LiveSessionMessages.DurationRequiredToStart);
        }

        if (view.State is "running" or "paused")
        {
            throw new ConcurrencyException(LiveSessionMessages.AlreadyStarted);
        }

        if (view.State is "ended" or "submitted")
        {
            throw new ConcurrencyException(LiveSessionMessages.Ended);
        }

        if (view.State != "lobby" || !LivePauseRules.JoinedRecently(view.CandidateLastSeenAtUtc, _clock.UtcNow))
        {
            throw new ConcurrencyException(LiveSessionMessages.CandidateNotJoined);
        }

        var attemptId = await _clockPort.StartForHostAsync(assignmentId, cancellationToken);
        await _sessions.MarkRunningAsync(assignmentId, attemptId, actorUserId, cancellationToken);
        _logger.LogInformation(
            "Live session {Action} assignment {AssignmentId} attempt {AttemptId} actor {ActorUserId}",
            LiveSessionActionNames.Started,
            assignmentId,
            attemptId,
            actorUserId);
        return await ProgressAsync(assignmentId, cancellationToken, applyLazy: false);
    }

    public async Task<LiveProgressResponse> PauseAsync(
        Guid assignmentId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        using var activity = AssignmentService.ActivitySource.StartActivity("live.pause");
        activity?.SetTag("assignment.id", assignmentId);
        await _clockPort.ApplyLazyAsync(assignmentId, cancellationToken);
        var view = await _sessions.GetRequiredAsync(assignmentId, cancellationToken);
        EnsureLive(view);
        if (view.State != "running")
        {
            throw new ConcurrencyException(LiveSessionMessages.NotRunning);
        }

        var pausedAt = await _clockPort.PauseAsync(assignmentId, cancellationToken);
        var facts = await _clockPort.GetFactsAsync(assignmentId, cancellationToken);
        await _sessions.MarkPausedAsync(assignmentId, facts?.AttemptId, pausedAt, actorUserId, cancellationToken);
        return await ProgressAsync(assignmentId, cancellationToken, applyLazy: false);
    }

    public async Task<LiveProgressResponse> ResumeAsync(
        Guid assignmentId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        using var activity = AssignmentService.ActivitySource.StartActivity("live.resume");
        activity?.SetTag("assignment.id", assignmentId);
        await _clockPort.ApplyLazyAsync(assignmentId, cancellationToken);
        var view = await _sessions.GetRequiredAsync(assignmentId, cancellationToken);
        EnsureLive(view);
        if (view.State != "paused")
        {
            throw new ConcurrencyException(LiveSessionMessages.NotPaused);
        }

        var seconds = await _clockPort.ResumeAsync(assignmentId, cancellationToken);
        var facts = await _clockPort.GetFactsAsync(assignmentId, cancellationToken);
        await _sessions.MarkResumedAsync(assignmentId, facts?.AttemptId, seconds, actorUserId, cancellationToken);
        return await ProgressAsync(assignmentId, cancellationToken, applyLazy: false);
    }

    public async Task<LiveProgressResponse> EndAsync(
        Guid assignmentId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        using var activity = AssignmentService.ActivitySource.StartActivity("live.end");
        activity?.SetTag("assignment.id", assignmentId);
        await _clockPort.ApplyLazyAsync(assignmentId, cancellationToken);
        var view = await _sessions.GetRequiredAsync(assignmentId, cancellationToken);
        EnsureLive(view);
        if (view.State is not ("ended" or "submitted"))
        {
            var facts = await _clockPort.GetFactsAsync(assignmentId, cancellationToken);
            await _sessions.MarkHostEndedAsync(assignmentId, facts?.AttemptId, actorUserId, cancellationToken);
            await _clockPort.ForceSubmitAsync(assignmentId, cancellationToken);
        }

        return await ProgressAsync(assignmentId, cancellationToken, applyLazy: false);
    }

    public async Task<LiveProgressResponse> ProgressAsync(
        Guid assignmentId,
        CancellationToken cancellationToken,
        bool applyLazy = true)
    {
        using var activity = AssignmentService.ActivitySource.StartActivity("live.progress");
        activity?.SetTag("assignment.id", assignmentId);
        if (applyLazy)
        {
            await _clockPort.ApplyLazyAsync(assignmentId, cancellationToken);
        }

        var view = await _sessions.GetRequiredAsync(assignmentId, cancellationToken);
        EnsureLive(view);
        var facts = await _clockPort.GetFactsAsync(assignmentId, cancellationToken);
        return Map(view, facts);
    }

    public Task SetPositionAsync(Guid assignmentId, int questionIndex, CancellationToken cancellationToken)
        => _sessions.SetPositionAsync(assignmentId, questionIndex, cancellationToken);

    private LiveProgressResponse Map(LiveSessionView view, AttemptLiveFacts? facts)
    {
        var ended = view.State is "ended" or "submitted";
        return new LiveProgressResponse
        {
            AssignmentId = view.AssignmentId,
            AttemptId = facts?.AttemptId,
            State = view.State,
            CandidateConnected = LivePauseRules.IsConnected(view.CandidateLastSeenAtUtc, _clock.UtcNow),
            LastActivityAtUtc = view.LastActivityAtUtc,
            CurrentQuestionIndex = view.CurrentQuestionIndex,
            QuestionCount = view.QuestionCount,
            AnsweredCount = facts?.AnsweredCount ?? 0,
            FlaggedCount = facts?.FlaggedCount ?? 0,
            RemainingSeconds = ended ? null : facts?.RemainingSeconds,
            StartedAtUtc = facts?.StartedAtUtc ?? view.LiveStartedAtUtc,
            PausedAtUtc = view.PausedAtUtc,
            TotalPausedSeconds = view.TotalPausedSeconds,
            EndedAtUtc = view.LiveEndedAtUtc,
            EndReason = view.EndReason
        };
    }

    private static void EnsureLive(LiveSessionView view)
    {
        if (!string.Equals(view.Mode, "live", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(LiveSessionMessages.NotLive);
        }
    }
}
