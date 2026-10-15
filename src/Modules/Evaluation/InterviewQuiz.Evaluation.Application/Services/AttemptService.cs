using System.Diagnostics;
using System.Text.Json;
using InterviewQuiz.Catalog.Application.CandidatePreview;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Ai;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Scoring;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Evaluation.Application.Services;

public sealed class AttemptService : IAttemptService, IAttemptClock, ICandidateAttemptIdLookup, IRevokedInviteAttemptCloser
{
    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Evaluation");

    private readonly IAttemptRepository _attempts;
    private readonly IAssignmentSnapshotReader _assignments;
    private readonly IAssignmentLifecycle _lifecycle;
    private readonly IInviteContinuation _invites;
    private readonly ILiveSessionGateway? _live;
    private readonly IClock _clock;
    private readonly ILogger<AttemptService> _logger;
    private readonly IAiSuggestionClient _suggestions;

    public AttemptService(
        IAttemptRepository attempts,
        IAssignmentSnapshotReader assignments,
        IAssignmentLifecycle lifecycle,
        IClock clock,
        ILogger<AttemptService> logger,
        IInviteContinuation? invites = null,
        ILiveSessionGateway? live = null,
        IAiSuggestionClient? suggestions = null)
    {
        _attempts = attempts;
        _assignments = assignments;
        _lifecycle = lifecycle;
        _invites = invites ?? AllowContinuingInvite.Instance;
        _live = live;
        _clock = clock;
        _logger = logger;
        _suggestions = suggestions ?? DisabledAiSuggestionClient.Instance;
    }

    public async Task<(CandidateAttemptResponse Response, bool Created)> StartAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.start");
        activity?.SetTag("assignment.id", assignmentId);
        await EnsureNotRevokedAsync(assignmentId, cancellationToken);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        EnsureAsync(header);

        var inProgress = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        if (inProgress is not null)
        {
            var expiredInPlace = inProgress.IsDue(_clock);
            inProgress = await AutoSubmitIfDueAsync(inProgress, header, cancellationToken);
            if (!inProgress.IsSubmitted || expiredInPlace)
            {
                return (MapCandidate(inProgress, header.Snapshot), false);
            }
        }

        var submittedCount = await _attempts.CountSubmittedAsync(assignmentId, cancellationToken);
        if (IsClosed(header.Status) || submittedCount >= header.AttemptLimit)
        {
            throw new ConcurrencyException(Attempt.LimitReachedMessage);
        }

        var duration = header.OverallDurationMinutes
            ?? throw new DomainException("Overall duration is required for async assignments.");

        var promptOrder = CandidateQuestionRedactor.BuildPromptOrder(header.Snapshot);
        var display = AttemptDisplayOrder.Create(header.Snapshot);
        var attempt = Attempt.Start(
            header.Id,
            header.OpeningId,
            header.SnapshotId,
            header.CandidateEmail,
            duration,
            promptOrder,
            _clock,
            displaySeed: display.Seed,
            displayOrder: display.Order);

        await _attempts.AddAsync(attempt, cancellationToken);
        try
        {
            await _attempts.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException ex) when (ex.Message == Attempt.InProgressConflictMessage)
        {
            var raced = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
            if (raced is not null && !raced.IsSubmitted)
            {
                return (MapCandidate(raced, header.Snapshot), false);
            }

            throw;
        }

        await _lifecycle.NotifyAttemptStarted(assignmentId, attempt.Id, cancellationToken);

        activity?.SetTag("attempt.id", attempt.Id);
        _logger.LogInformation(
            "Started attempt {AttemptId} for assignment {AssignmentId} snapshot {SnapshotId}",
            attempt.Id,
            assignmentId,
            header.SnapshotId);

        return (MapCandidate(attempt, header.Snapshot), true);
    }

    public async Task<CandidateAttemptPreviewResponse> GetPreviewAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.preview");
        activity?.SetTag("assignment.id", assignmentId);
        await EnsureNotRevokedAsync(assignmentId, cancellationToken);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        if (IsLive(header))
        {
            await TouchLiveAsync(assignmentId, cancellationToken);
        }

        var inProgress = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        string attemptStatus;
        if (inProgress is not null)
        {
            attemptStatus = "inProgress";
        }
        else
        {
            var latest = await _attempts.GetLatestAsync(assignmentId, cancellationToken);
            attemptStatus = latest is null ? "notStarted" : "submitted";
        }

        var openingName = string.IsNullOrWhiteSpace(header.OpeningTitle) ? "Opening" : header.OpeningTitle.Trim();
        var live = IsLive(header) ? await LiveViewAsync(assignmentId, cancellationToken) : null;
        return new CandidateAttemptPreviewResponse
        {
            AttemptStatus = attemptStatus,
            QuizTitle = header.Snapshot.Title,
            OpeningName = openingName,
            DurationMinutes = header.OverallDurationMinutes ?? 0,
            QuestionCount = header.Snapshot.Questions.Count,
            LiveState = MapPreviewLiveState(live)
        };
    }

    public async Task<CandidateAttemptResponse> GetCurrentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.get");
        activity?.SetTag("assignment.id", assignmentId);
        await EnsureNotRevokedAsync(assignmentId, cancellationToken);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        if (IsLive(header))
        {
            await ApplyLazyAsync(assignmentId, cancellationToken);
            await TouchLiveAsync(assignmentId, cancellationToken);
        }

        var attempt = await RequireCurrentAsync(assignmentId, cancellationToken);
        if (!IsLive(header))
        {
            attempt = await AutoSubmitIfDueAsync(attempt, header, cancellationToken);
        }

        var live = IsLive(header) ? await LiveViewAsync(assignmentId, cancellationToken) : null;
        return MapCandidate(attempt, header.Snapshot, live);
    }

    public async Task<CandidateAttemptResponse> SaveAnswersAsync(
        Guid assignmentId,
        SaveAnswersRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.save");
        activity?.SetTag("assignment.id", assignmentId);
        await EnsureNotRevokedAsync(assignmentId, cancellationToken);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        if (IsLive(header))
        {
            await ApplyLazyAsync(assignmentId, cancellationToken);
            await TouchLiveAsync(assignmentId, cancellationToken);
        }

        var attempt = await RequireCurrentAsync(assignmentId, cancellationToken);
        if (!IsLive(header))
        {
            attempt = await AutoSubmitIfDueAsync(attempt, header, cancellationToken);
        }

        if (attempt.IsSubmitted)
        {
            throw new ConcurrencyException(Attempt.AlreadySubmittedMessage);
        }

        if (attempt.IsPaused)
        {
            throw new ConcurrencyException(LiveSessionMessages.Paused);
        }

        var known = header.Snapshot.Questions.Select(q => q.Id).ToHashSet();
        var upserts = (request.Answers ?? [])
            .Select(a => (a.QuestionId, CloneValue(a.Value)))
            .ToList();
        attempt.UpsertAnswers(upserts, known);
        await _attempts.SaveChangesAsync(cancellationToken);

        activity?.SetTag("attempt.id", attempt.Id);
        _logger.LogInformation(
            "Saved answers for attempt {AttemptId} assignment {AssignmentId}",
            attempt.Id,
            assignmentId);

        var live = IsLive(header) ? await LiveViewAsync(assignmentId, cancellationToken) : null;
        return MapCandidate(attempt, header.Snapshot, live);
    }

    public async Task<CandidateSubmitResponse> SubmitAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.submit");
        activity?.SetTag("assignment.id", assignmentId);
        await EnsureNotRevokedAsync(assignmentId, cancellationToken);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        if (IsLive(header))
        {
            await ApplyLazyAsync(assignmentId, cancellationToken);
        }

        var attempt = await RequireCurrentAsync(assignmentId, cancellationToken);

        if (!attempt.IsSubmitted)
        {
            await ScoreAndSubmitAsync(attempt, header, cancellationToken);
        }
        else
        {
            throw new ConcurrencyException(Attempt.AlreadySubmittedMessage);
        }

        return MapSubmit(attempt);
    }

    public Task ApplyLazyAsync(Guid assignmentId, CancellationToken cancellationToken)
        => ApplyLiveClockAsync(assignmentId, cancellationToken);

    public async Task<Guid> StartForHostAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("live.start");
        activity?.SetTag("assignment.id", assignmentId);
        await EnsureNotRevokedAsync(assignmentId, cancellationToken);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        if (!IsLive(header))
        {
            throw new DomainException(LiveSessionMessages.NotLive);
        }

        var duration = header.OverallDurationMinutes
            ?? throw new DomainException(LiveSessionMessages.DurationRequiredToStart);

        var existing = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        if (existing is not null && !existing.IsSubmitted)
        {
            throw new ConcurrencyException(LiveSessionMessages.AlreadyStarted);
        }

        var promptOrder = CandidateQuestionRedactor.BuildPromptOrder(header.Snapshot);
        var display = AttemptDisplayOrder.Create(header.Snapshot);
        var attempt = Attempt.Start(
            header.Id,
            header.OpeningId,
            header.SnapshotId,
            header.CandidateEmail,
            duration,
            promptOrder,
            _clock,
            displaySeed: display.Seed,
            displayOrder: display.Order);

        await _attempts.AddAsync(attempt, cancellationToken);
        try
        {
            await _attempts.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException ex) when (ex.Message == Attempt.InProgressConflictMessage)
        {
            throw new ConcurrencyException(LiveSessionMessages.AlreadyStarted);
        }

        await _lifecycle.NotifyAttemptStarted(assignmentId, attempt.Id, cancellationToken);
        activity?.SetTag("attempt.id", attempt.Id);
        _logger.LogInformation(
            "Host started attempt {AttemptId} for assignment {AssignmentId}",
            attempt.Id,
            assignmentId);
        return attempt.Id;
    }

    public async Task<DateTimeOffset> PauseAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var attempt = await RequireInProgressAsync(assignmentId, cancellationToken);
        var pausedAt = attempt.Pause(_clock);
        await _attempts.SaveChangesAsync(cancellationToken);
        return pausedAt;
    }

    public async Task<int> ResumeAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var attempt = await RequireInProgressAsync(assignmentId, cancellationToken);
        var seconds = attempt.Resume(_clock);
        await _attempts.SaveChangesAsync(cancellationToken);
        return seconds;
    }

    public async Task ForceSubmitAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        var attempt = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        if (attempt is null || attempt.IsSubmitted)
        {
            return;
        }

        await ScoreAndSubmitAsync(attempt, header, cancellationToken);
    }

    public async Task<AttemptLiveFacts?> GetFactsAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var attempt = await _attempts.GetInProgressAsync(assignmentId, cancellationToken)
            ?? await _attempts.GetLatestAsync(assignmentId, cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        return new AttemptLiveFacts(
            attempt.Id,
            attempt.IsSubmitted,
            attempt.IsPaused,
            attempt.IsSubmitted ? null : attempt.RemainingSeconds(_clock),
            attempt.StartedAtUtc,
            attempt.DueAtUtc,
            attempt.Answers.Count(a => a.HasValue),
            attempt.Answers.Count(a => a.Flagged));
    }

    public async Task SetFlagsAsync(
        Guid assignmentId,
        IReadOnlyList<ReviewFlagUpdate> flags,
        CancellationToken cancellationToken)
    {
        await EnsureNotRevokedAsync(assignmentId, cancellationToken);
        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        if (IsLive(header))
        {
            await ApplyLiveClockAsync(assignmentId, cancellationToken);
        }

        var attempt = await RequireCurrentAsync(assignmentId, cancellationToken);
        if (attempt.IsSubmitted)
        {
            throw new ConcurrencyException(Attempt.AlreadySubmittedMessage);
        }

        if (attempt.IsPaused)
        {
            throw new ConcurrencyException(LiveSessionMessages.Paused);
        }

        var known = header.Snapshot.Questions.Select(q => q.Id).ToHashSet();
        attempt.SetFlags(flags.Select(f => (f.QuestionId, f.Flagged)).ToList(), known);
        await _attempts.SaveChangesAsync(cancellationToken);
        if (IsLive(header))
        {
            await TouchLiveAsync(assignmentId, cancellationToken);
        }
    }

    public async Task<PagedResult<AttemptSummaryResponse>> ListByAssignmentAsync(
        Guid assignmentId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        _ = await RequireAssignmentAsync(assignmentId, cancellationToken);
        var result = await _attempts.ListByAssignmentAsync(assignmentId, page, cancellationToken);
        var items = result.Items.Select(MapSummary).ToList();
        return new PagedResult<AttemptSummaryResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<AttemptResultResponse> GetResultAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.get");
        activity?.SetTag("attempt.id", attemptId);

        var attempt = await _attempts.GetAsync(attemptId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Attempt), attemptId);

        var header = await RequireAssignmentAsync(attempt.AssignmentId, cancellationToken);
        return MapResult(attempt, header.Snapshot);
    }

    public async Task<AttemptReviewResponse> GetReviewAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.review.get");
        activity?.SetTag("attempt.id", attemptId);

        var attempt = await RequireSubmittedAttemptAsync(attemptId, cancellationToken);
        var header = await RequireAssignmentAsync(attempt.AssignmentId, cancellationToken);
        return MapReview(attempt, header.Snapshot, _suggestions.IsEnabled);
    }

    public async Task<AttemptReviewResponse> SuggestItemAsync(
        Guid attemptId,
        Guid questionId,
        string reviewerUserId,
        CancellationToken cancellationToken)
    {
        if (!_suggestions.IsEnabled)
        {
            throw new DomainException(Attempt.SuggestionUnavailableMessage);
        }

        using var activity = ActivitySource.StartActivity("attempts.review.suggest");
        activity?.SetTag("attempt.id", attemptId);
        activity?.SetTag("question.id", questionId);

        var attempt = await RequireSubmittedAttemptAsync(attemptId, cancellationToken);
        activity?.SetTag("assignment.id", attempt.AssignmentId);
        var item = attempt.ItemResults.FirstOrDefault(result => result.QuestionId == questionId)
            ?? throw new DomainException(Attempt.UnknownQuestionMessage);
        if (!item.IsAiAssist)
        {
            throw new DomainException(Attempt.SuggestionOnlyForAiAssistMessage);
        }

        var header = await RequireAssignmentAsync(attempt.AssignmentId, cancellationToken);
        var question = header.Snapshot.Questions.FirstOrDefault(q => q.Id == questionId);
        var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == questionId);
        JsonElement? candidateAnswer = answer is null ? null : answer.Value.RootElement.Clone();
        var draft = await _suggestions.SuggestAsync(
            new AiSuggestionRequest(
                questionId,
                item.Type,
                item.Points,
                question?.Stem ?? "",
                question?.Body ?? default,
                candidateAnswer),
            cancellationToken);

        attempt.RecordSuggestion(
            questionId,
            draft.Points,
            draft.Feedback,
            reviewerUserId,
            _suggestions.ModelName,
            _clock);
        await _attempts.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Stored AI suggestion model {Model} for question {QuestionId} on attempt {AttemptId}",
            _suggestions.ModelName,
            questionId,
            attempt.Id);

        return MapReview(attempt, header.Snapshot, _suggestions.IsEnabled);
    }

    public async Task<AttemptReviewResponse> ScoreItemAsync(
        Guid attemptId,
        Guid questionId,
        ReviewItemRequest request,
        string reviewerUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = ActivitySource.StartActivity("attempts.review");
        activity?.SetTag("attempt.id", attemptId);
        activity?.SetTag("question.id", questionId);

        var attempt = await RequireSubmittedAttemptAsync(attemptId, cancellationToken);
        activity?.SetTag("assignment.id", attempt.AssignmentId);
        var newlyComplete = attempt.ApplyHumanScore(
            questionId,
            request.PointsAwarded,
            reviewerUserId,
            request.Comment,
            _clock);
        await _attempts.SaveChangesAsync(cancellationToken);
        if (newlyComplete)
        {
            await _lifecycle.NotifyReviewCompleted(attempt.AssignmentId, attempt.Id, cancellationToken);
        }

        _logger.LogInformation(
            "Reviewed item {QuestionId} on attempt {AttemptId} for assignment {AssignmentId}",
            questionId,
            attempt.Id,
            attempt.AssignmentId);

        var header = await RequireAssignmentAsync(attempt.AssignmentId, cancellationToken);
        return MapReview(attempt, header.Snapshot, _suggestions.IsEnabled);
    }

    private async Task<Attempt> RequireSubmittedAttemptAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        var attempt = await _attempts.GetAsync(attemptId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Attempt), attemptId);
        if (!attempt.IsSubmitted)
        {
            throw new ConcurrencyException(Attempt.NotSubmittedMessage);
        }

        return attempt;
    }

    public async Task CloseInProgressAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.revoke");
        activity?.SetTag("assignment.id", assignmentId);

        var inProgress = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        if (inProgress is null || inProgress.IsSubmitted)
        {
            return;
        }

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        await ScoreAndSubmitAsync(inProgress, header, cancellationToken);
        _logger.LogInformation(
            "Ended in-progress attempt {AttemptId} because the invite for assignment {AssignmentId} was revoked",
            inProgress.Id,
            assignmentId);
    }

    public async Task<Guid?> GetAttemptIdAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var inProgress = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        if (inProgress is not null)
        {
            return inProgress.Id;
        }

        var latest = await _attempts.GetLatestAsync(assignmentId, cancellationToken);
        return latest?.Id;
    }

    private async Task<Attempt> AutoSubmitIfDueAsync(
        Attempt attempt,
        AssignmentWithSnapshotDto header,
        CancellationToken cancellationToken)
    {
        if (attempt.IsSubmitted || !attempt.IsDue(_clock))
        {
            return attempt;
        }

        await ScoreAndSubmitAsync(attempt, header, cancellationToken);
        return attempt;
    }

    private async Task ScoreAndSubmitAsync(
        Attempt attempt,
        AssignmentWithSnapshotDto header,
        CancellationToken cancellationToken)
    {
        using var score = ActivitySource.StartActivity("attempts.score");
        score?.SetTag("attempt.id", attempt.Id);
        score?.SetTag("assignment.id", attempt.AssignmentId);

        var answers = attempt.Answers
            .Where(a => a.HasValue)
            .ToDictionary(a => a.QuestionId, a => a.Value.RootElement.Clone());
        var scores = AutoScorer.ScoreAttempt(header.Snapshot, answers);

        var itemResults = scores.Select(s => AttemptItemResult.Create(
            attempt.Id,
            s.QuestionId,
            s.SortOrder,
            s.Type,
            s.ScoringMode,
            s.Points,
            s.Status,
            s.PointsAwarded)).ToList();

        var auto = scores.Where(s => s.Status == ItemScoreStatus.Scored).ToList();
        var autoAwarded = auto.Sum(s => s.PointsAwarded ?? 0m);
        var autoAvailable = scores
            .Where(s => string.Equals(s.ScoringMode, "auto", StringComparison.OrdinalIgnoreCase))
            .Sum(s => (decimal)s.Points);
        var totalAvailable = scores.Sum(s => (decimal)s.Points);
        var unsettled = scores.Any(s => s.Status == ItemScoreStatus.Unsettled);
        var resultStatus = unsettled ? Domain.ResultStatus.Incomplete : Domain.ResultStatus.Complete;

        attempt.Submit(itemResults, resultStatus, autoAwarded, autoAvailable, totalAvailable, _clock);
        await _attempts.SaveChangesAsync(cancellationToken);

        var notifyStatus = resultStatus == Domain.ResultStatus.Complete
            ? AttemptResultStatuses.Complete
            : AttemptResultStatuses.Incomplete;
        await _lifecycle.NotifyAttemptSubmitted(attempt.AssignmentId, attempt.Id, notifyStatus, cancellationToken);

        _logger.LogInformation(
            "Scored attempt {AttemptId} for assignment {AssignmentId} snapshot {SnapshotId}",
            attempt.Id,
            attempt.AssignmentId,
            attempt.SnapshotId);
    }

    private async Task EnsureNotRevokedAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (await _invites.IsRevokedAsync(assignmentId, cancellationToken))
        {
            throw new DomainException(InviteMessages.Revoked);
        }
    }

    private async Task<AssignmentWithSnapshotDto> RequireAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        return await _assignments.GetAssignmentWithSnapshotAsync(assignmentId, cancellationToken)
            ?? throw new EntityNotFoundException("Assignment", assignmentId);
    }

    private async Task<Attempt> RequireCurrentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var inProgress = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        if (inProgress is not null)
        {
            return inProgress;
        }

        var latest = await _attempts.GetLatestAsync(assignmentId, cancellationToken);
        return latest ?? throw new EntityNotFoundException(nameof(Attempt), assignmentId);
    }

    private async Task ApplyLiveClockAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (_live is null)
        {
            return;
        }

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        if (!IsLive(header))
        {
            return;
        }

        if (await _live.TryEndForPauseLimitAsync(assignmentId, cancellationToken))
        {
            var ended = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
            if (ended is not null && !ended.IsSubmitted)
            {
                await ScoreAndSubmitAsync(ended, header, cancellationToken);
            }

            return;
        }

        var current = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        if (current is null || current.IsSubmitted || current.IsPaused || !current.IsDue(_clock))
        {
            return;
        }

        await _live.MarkAutoEndedAsync(assignmentId, LiveEndReasons.Timeout, current.Id, cancellationToken);
        await ScoreAndSubmitAsync(current, header, cancellationToken);
    }

    private async Task TouchLiveAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (_live is not null)
        {
            await _live.TouchAsync(assignmentId, cancellationToken);
        }
    }

    private async Task<LiveSessionView?> LiveViewAsync(Guid assignmentId, CancellationToken cancellationToken)
        => _live is null ? null : await _live.GetAsync(assignmentId, cancellationToken);

    private async Task<Attempt> RequireInProgressAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var attempt = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        return attempt is null || attempt.IsSubmitted
            ? throw new EntityNotFoundException(nameof(Attempt), assignmentId)
            : attempt;
    }

    private static bool IsLive(AssignmentWithSnapshotDto header)
        => string.Equals(header.Mode, "live", StringComparison.OrdinalIgnoreCase);

    private static string? MapPreviewLiveState(LiveSessionView? live)
    {
        if (live is null)
        {
            return null;
        }

        return live.State switch
        {
            "running" => "running",
            "paused" => "paused",
            "ended" or "submitted" => "ended",
            _ => "waiting"
        };
    }

    private static string? MapCandidateLiveState(LiveSessionView? live, Attempt attempt)
    {
        if (live is null)
        {
            return null;
        }

        if (attempt.IsSubmitted)
        {
            return live.EndReason is LiveEndReasons.Host or LiveEndReasons.Timeout or LiveEndReasons.PauseLimit
                ? "ended"
                : "submitted";
        }

        return live.State is "ended" or "submitted" ? "ended" : live.State;
    }

    private static void EnsureAsync(AssignmentWithSnapshotDto header)
    {
        if (!string.Equals(header.Mode, "async", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(LiveSessionMessages.WaitingForHost);
        }
    }

    private static bool IsClosed(string status)
        => status.Equals("pendingReview", StringComparison.OrdinalIgnoreCase)
           || status.Equals("completed", StringComparison.OrdinalIgnoreCase);

    private CandidateAttemptResponse MapCandidate(
        Attempt attempt,
        QuizSnapshotDto snapshot,
        LiveSessionView? live = null)
    {
        var promptOrder = CandidateQuestionRedactor.ParsePromptOrder(attempt.PromptOrderJson);
        var optionOrder = AttemptDisplayOrder.ParseOptionOrder(attempt.DisplayOrderJson);
        var questions = AttemptDisplayOrder.OrderQuestions(snapshot.Questions, attempt.DisplayOrderJson)
            .Select(q => new CandidateQuestionDto
            {
                Id = q.Id,
                SortOrder = q.SortOrder,
                Type = q.Type,
                Stem = q.Stem,
                Body = CandidateQuestionRedactor.RedactBody(q, promptOrder, optionOrder),
                SourceQuestionId = q.SourceQuestionId
            })
            .ToList();

        var answers = attempt.Answers
            .Where(a => a.HasValue)
            .Select(a => new AnswerDto
            {
                QuestionId = a.QuestionId,
                Value = a.Value.RootElement.Clone()
            })
            .ToList();

        IReadOnlyList<CandidateItemResultDto>? itemResults = null;
        if (attempt.IsSubmitted)
        {
            itemResults = attempt.ItemResults
                .Select(r => new CandidateItemResultDto
                {
                    QuestionId = r.QuestionId,
                    Status = Camel(r.Status),
                    PointsAwarded = r.PointsAwarded
                })
                .ToList();
        }

        return new CandidateAttemptResponse
        {
            Id = attempt.Id,
            AssignmentId = attempt.AssignmentId,
            Status = Camel(attempt.Status),
            StartedAtUtc = attempt.StartedAtUtc,
            DueAtUtc = attempt.DueAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc,
            RemainingSeconds = attempt.RemainingSeconds(_clock),
            Questions = questions,
            Answers = answers,
            ItemResults = itemResults,
            LiveState = MapCandidateLiveState(live, attempt),
            Paused = attempt.IsPaused || string.Equals(live?.State, "paused", StringComparison.Ordinal),
            LiveEndReason = live?.EndReason,
            FlaggedQuestionIds = attempt.Answers.Where(a => a.Flagged).Select(a => a.QuestionId).ToList()
        };
    }

    private static CandidateSubmitResponse MapSubmit(Attempt attempt)
        => new()
        {
            Id = attempt.Id,
            AssignmentId = attempt.AssignmentId,
            Status = Camel(attempt.Status),
            ResultStatus = attempt.ResultStatus is { } status ? Camel(status) : AttemptResultStatuses.Incomplete,
            StartedAtUtc = attempt.StartedAtUtc,
            DueAtUtc = attempt.DueAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc,
            AutoPointsAwarded = attempt.AutoPointsAwarded,
            AutoPointsAvailable = attempt.AutoPointsAvailable,
            TotalPointsAvailable = attempt.TotalPointsAvailable,
            ItemResults = attempt.ItemResults.Select(r => new CandidateItemResultDto
            {
                QuestionId = r.QuestionId,
                Status = Camel(r.Status),
                PointsAwarded = r.PointsAwarded
            }).ToList()
        };

    private static AttemptSummaryResponse MapSummary(Attempt attempt)
        => new()
        {
            Id = attempt.Id,
            AssignmentId = attempt.AssignmentId,
            OpeningId = attempt.OpeningId,
            CandidateEmail = attempt.CandidateEmail,
            Status = Camel(attempt.Status),
            ResultStatus = attempt.ResultStatus is { } status ? Camel(status) : AttemptResultStatuses.Incomplete,
            StartedAtUtc = attempt.StartedAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc,
            AutoPointsAwarded = attempt.AutoPointsAwarded,
            AutoPointsAvailable = attempt.AutoPointsAvailable,
            TotalPointsAvailable = attempt.TotalPointsAvailable,
            FinalPointsAwarded = attempt.FinalPointsAwarded
        };

    private static AttemptResultResponse MapResult(Attempt attempt, QuizSnapshotDto snapshot)
    {
        var questions = snapshot.Questions.ToDictionary(q => q.Id);
        var answers = attempt.Answers.ToDictionary(a => a.QuestionId, a => a.Value.RootElement.Clone());
        var emptyOrder = new Dictionary<Guid, IReadOnlyList<string>>();
        var items = attempt.ItemResults
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.QuestionId)
            .Select(r =>
            {
                questions.TryGetValue(r.QuestionId, out var question);
                return new AttemptResultItemDto
                {
                    QuestionId = r.QuestionId,
                    SortOrder = r.SortOrder,
                    Type = r.Type,
                    ScoringMode = r.ScoringMode,
                    Points = r.Points,
                    Status = Camel(r.Status),
                    PointsAwarded = r.PointsAwarded,
                    CandidateAnswer = answers.TryGetValue(r.QuestionId, out var value) ? value : null,
                    Stem = question?.Stem,
                    Body = question is null
                        ? null
                        : CandidateQuestionRedactor.RedactBody(question, emptyOrder)
                };
            })
            .ToList();

        return new AttemptResultResponse
        {
            Id = attempt.Id,
            AssignmentId = attempt.AssignmentId,
            OpeningId = attempt.OpeningId,
            CandidateEmail = attempt.CandidateEmail,
            Status = Camel(attempt.Status),
            ResultStatus = attempt.ResultStatus is { } status ? Camel(status) : AttemptResultStatuses.Incomplete,
            StartedAtUtc = attempt.StartedAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc,
            AutoPointsAwarded = attempt.AutoPointsAwarded,
            AutoPointsAvailable = attempt.AutoPointsAvailable,
            TotalPointsAvailable = attempt.TotalPointsAvailable,
            FinalPointsAwarded = attempt.FinalPointsAwarded,
            QuizRowVersion = snapshot.RowVersion,
            Items = items
        };
    }

    private static AttemptReviewResponse MapReview(Attempt attempt, QuizSnapshotDto snapshot, bool aiSuggestionsEnabled)
    {
        var questions = snapshot.Questions.ToDictionary(q => q.Id);
        var answers = attempt.Answers.ToDictionary(a => a.QuestionId, a => a.Value.RootElement.Clone());
        var emptyOrder = new Dictionary<Guid, IReadOnlyList<string>>();
        var items = attempt.ItemResults
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.QuestionId)
            .Select(r =>
            {
                questions.TryGetValue(r.QuestionId, out var question);
                return new AttemptReviewItemDto
                {
                    QuestionId = r.QuestionId,
                    SortOrder = r.SortOrder,
                    Type = r.Type,
                    ScoringMode = r.ScoringMode,
                    Points = r.Points,
                    Status = Camel(r.Status),
                    PointsAwarded = r.PointsAwarded,
                    CandidateAnswer = answers.TryGetValue(r.QuestionId, out var value) ? value : null,
                    Stem = question?.Stem,
                    Body = question is null
                        ? null
                        : CandidateQuestionRedactor.RedactBody(question, emptyOrder),
                    AnswerKey = question is null ? null : question.Body.Clone(),
                    Reviewable = r.IsHumanReviewable,
                    ReviewedByUserId = r.ReviewedByUserId,
                    ReviewedAtUtc = r.ReviewedAtUtc,
                    ReviewComment = r.ReviewComment,
                    SuggestedPoints = r.SuggestedPoints,
                    SuggestionFeedback = r.SuggestionFeedback,
                    SuggestedByUserId = r.SuggestedByUserId,
                    SuggestedAtUtc = r.SuggestedAtUtc,
                    SuggestionModel = r.SuggestionModel
                };
            })
            .ToList();

        return new AttemptReviewResponse
        {
            Id = attempt.Id,
            AssignmentId = attempt.AssignmentId,
            OpeningId = attempt.OpeningId,
            CandidateEmail = attempt.CandidateEmail,
            Status = Camel(attempt.Status),
            ResultStatus = attempt.ResultStatus is { } status ? Camel(status) : AttemptResultStatuses.Incomplete,
            StartedAtUtc = attempt.StartedAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc,
            AutoPointsAwarded = attempt.AutoPointsAwarded,
            AutoPointsAvailable = attempt.AutoPointsAvailable,
            TotalPointsAvailable = attempt.TotalPointsAvailable,
            FinalPointsAwarded = attempt.FinalPointsAwarded,
            AiSuggestionsEnabled = aiSuggestionsEnabled,
            QuizRowVersion = snapshot.RowVersion,
            Items = items
        };
    }

    private static JsonDocument CloneValue(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new DomainException("Answer value is required.");
        }

        return JsonDocument.Parse(value.GetRawText());
    }

    private static string Camel(Enum value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private sealed class AllowContinuingInvite : IInviteContinuation
    {
        public static readonly AllowContinuingInvite Instance = new();

        public Task<bool> IsRevokedAsync(Guid assignmentId, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }
}
