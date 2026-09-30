using System.Diagnostics;
using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Scoring;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Evaluation.Application.Services;

public sealed class AttemptService : IAttemptService, ICandidateAttemptIdLookup
{
    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Evaluation");

    private readonly IAttemptRepository _attempts;
    private readonly IAssignmentSnapshotReader _assignments;
    private readonly IAssignmentLifecycle _lifecycle;
    private readonly IClock _clock;
    private readonly ILogger<AttemptService> _logger;

    public AttemptService(
        IAttemptRepository attempts,
        IAssignmentSnapshotReader assignments,
        IAssignmentLifecycle lifecycle,
        IClock clock,
        ILogger<AttemptService> logger)
    {
        _attempts = attempts;
        _assignments = assignments;
        _lifecycle = lifecycle;
        _clock = clock;
        _logger = logger;
    }

    public async Task<(CandidateAttemptResponse Response, bool Created)> StartAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.start");
        activity?.SetTag("assignment.id", assignmentId);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        EnsureAsync(header);

        var inProgress = await _attempts.GetInProgressAsync(assignmentId, cancellationToken);
        if (inProgress is not null)
        {
            inProgress = await AutoSubmitIfDueAsync(inProgress, header, cancellationToken);
            if (!inProgress.IsSubmitted)
            {
                return (MapCandidate(inProgress, header.Snapshot), false);
            }
        }

        var submittedCount = await _attempts.CountSubmittedAsync(assignmentId, cancellationToken);
        if (submittedCount >= header.AttemptLimit)
        {
            throw new ConcurrencyException(Attempt.LimitReachedMessage);
        }

        var duration = header.OverallDurationMinutes
            ?? throw new DomainException("Overall duration is required for async assignments.");

        var promptOrder = CandidateQuestionRedactor.BuildPromptOrder(header.Snapshot);
        var attempt = Attempt.Start(
            header.Id,
            header.OpeningId,
            header.SnapshotId,
            header.CandidateEmail,
            duration,
            promptOrder,
            _clock);

        await _attempts.AddAsync(attempt, cancellationToken);
        await _attempts.SaveChangesAsync(cancellationToken);
        await _lifecycle.NotifyAttemptStarted(assignmentId, attempt.Id, cancellationToken);

        activity?.SetTag("attempt.id", attempt.Id);
        _logger.LogInformation(
            "Started attempt {AttemptId} for assignment {AssignmentId} snapshot {SnapshotId}",
            attempt.Id,
            assignmentId,
            header.SnapshotId);

        return (MapCandidate(attempt, header.Snapshot), true);
    }

    public async Task<CandidateAttemptResponse> GetCurrentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.get");
        activity?.SetTag("assignment.id", assignmentId);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        EnsureAsync(header);
        var attempt = await RequireCurrentAsync(assignmentId, cancellationToken);
        attempt = await AutoSubmitIfDueAsync(attempt, header, cancellationToken);
        return MapCandidate(attempt, header.Snapshot);
    }

    public async Task<CandidateAttemptResponse> SaveAnswersAsync(
        Guid assignmentId,
        SaveAnswersRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.save");
        activity?.SetTag("assignment.id", assignmentId);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        EnsureAsync(header);
        var attempt = await RequireCurrentAsync(assignmentId, cancellationToken);
        attempt = await AutoSubmitIfDueAsync(attempt, header, cancellationToken);
        if (attempt.IsSubmitted)
        {
            throw new ConcurrencyException(Attempt.AlreadySubmittedMessage);
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

        return MapCandidate(attempt, header.Snapshot);
    }

    public async Task<CandidateSubmitResponse> SubmitAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("attempts.submit");
        activity?.SetTag("assignment.id", assignmentId);

        var header = await RequireAssignmentAsync(assignmentId, cancellationToken);
        EnsureAsync(header);
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

        var answers = attempt.Answers.ToDictionary(
            a => a.QuestionId,
            a => a.Value.RootElement.Clone());
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

    private static void EnsureAsync(AssignmentWithSnapshotDto header)
    {
        if (!string.Equals(header.Mode, "async", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Assignment is live; start is not available.");
        }
    }

    private CandidateAttemptResponse MapCandidate(Attempt attempt, QuizSnapshotDto snapshot)
    {
        var promptOrder = CandidateQuestionRedactor.ParsePromptOrder(attempt.PromptOrderJson);
        var questions = snapshot.Questions
            .OrderBy(q => q.SortOrder)
            .ThenBy(q => q.Id)
            .Select(q => new CandidateQuestionDto
            {
                Id = q.Id,
                SortOrder = q.SortOrder,
                Type = q.Type,
                Stem = q.Stem,
                ScoringMode = q.ScoringMode,
                CreditMode = q.CreditMode,
                Points = q.Points,
                Body = CandidateQuestionRedactor.RedactBody(q, promptOrder),
                SourceQuestionId = q.SourceQuestionId
            })
            .ToList();

        var answers = attempt.Answers
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
                    ScoringMode = r.ScoringMode,
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
            ItemResults = itemResults
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
                ScoringMode = r.ScoringMode,
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
            TotalPointsAvailable = attempt.TotalPointsAvailable
        };

    private static AttemptResultResponse MapResult(Attempt attempt, QuizSnapshotDto snapshot)
    {
        var stems = snapshot.Questions.ToDictionary(q => q.Id, q => q.Stem);
        var answers = attempt.Answers.ToDictionary(a => a.QuestionId, a => a.Value.RootElement.Clone());
        var items = attempt.ItemResults
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.QuestionId)
            .Select(r => new AttemptResultItemDto
            {
                QuestionId = r.QuestionId,
                SortOrder = r.SortOrder,
                Type = r.Type,
                ScoringMode = r.ScoringMode,
                Points = r.Points,
                Status = Camel(r.Status),
                PointsAwarded = r.PointsAwarded,
                CandidateAnswer = answers.TryGetValue(r.QuestionId, out var value) ? value : null,
                Stem = stems.GetValueOrDefault(r.QuestionId)
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
}
