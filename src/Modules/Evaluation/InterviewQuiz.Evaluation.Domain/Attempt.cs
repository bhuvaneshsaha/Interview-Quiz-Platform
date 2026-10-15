using System.Text.Json;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Evaluation.Domain;

public sealed class Attempt
{
    public const string LimitReachedMessage = "Attempt limit reached.";
    public const string AlreadySubmittedMessage = "Attempt already submitted.";
    public const string InProgressConflictMessage = "Attempt already in progress.";
    public const string UnknownQuestionMessage = "Unknown question id.";
    public const string NotSubmittedMessage = "Attempt is not submitted.";
    public const string NotReviewableMessage = "Only human-only and AI-assist items can be reviewed.";
    public const string PointsOutOfRangeMessage = "Points awarded must be between 0 and the question points.";
    public const string CommentTooLongMessage = "Review comment must be 2000 characters or fewer.";
    public const string ReviewerRequiredMessage = "Reviewer is required.";
    public const string SuggestionOnlyForAiAssistMessage = "AI suggestions apply only to AI-assist items.";
    public const string SuggestionUnavailableMessage = "AI suggestions are not configured.";
    public const string SuggestionFailedMessage = "AI suggestion could not be produced.";
    public const string SuggestionFeedbackTooLongMessage = "Suggestion feedback must be 2000 characters or fewer.";
    public const int ReviewCommentMaxLength = 2000;
    public const int SuggestionFeedbackMaxLength = 2000;
    public const int SuggestionModelMaxLength = 200;

    private readonly List<AttemptAnswer> _answers = [];
    private readonly List<AttemptItemResult> _itemResults = [];

    private Attempt()
    {
        PromptOrderJson = JsonDocument.Parse("{}");
        DisplayOrderJson = JsonDocument.Parse("{}");
    }

    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid OpeningId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public string CandidateEmail { get; private set; } = "";
    public AttemptStatus Status { get; private set; }
    public ResultStatus? ResultStatus { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public DateTimeOffset? PausedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public decimal AutoPointsAwarded { get; private set; }
    public decimal AutoPointsAvailable { get; private set; }
    public decimal TotalPointsAvailable { get; private set; }

    /// <summary>
    /// Sum of awarded points once every item is settled. Null while any item is unsettled.
    /// </summary>
    public decimal? FinalPointsAwarded { get; private set; }
    public JsonDocument PromptOrderJson { get; private set; }

    /// <summary>
    /// Seed used once at start. The resolved order is <see cref="DisplayOrderJson"/>.
    /// </summary>
    public int DisplaySeed { get; private set; }

    public JsonDocument DisplayOrderJson { get; private set; }
    public int RowVersion { get; private set; }

    public IReadOnlyList<AttemptAnswer> Answers => _answers;
    public IReadOnlyList<AttemptItemResult> ItemResults => _itemResults;

    public bool IsSubmitted => Status == AttemptStatus.Submitted;

    public bool IsPaused => PausedAtUtc is not null;

    public static Attempt Start(
        Guid assignmentId,
        Guid openingId,
        Guid snapshotId,
        string candidateEmail,
        int overallDurationMinutes,
        JsonDocument promptOrder,
        IClock clock,
        Guid? id = null,
        int displaySeed = 0,
        JsonDocument? displayOrder = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(promptOrder);
        if (overallDurationMinutes < 1)
        {
            throw new DomainException("Overall duration is required for async assignments.");
        }

        // timestamptz keeps microseconds. Truncate once so start and due survive a reload.
        var now = TruncateToMicroseconds(clock.UtcNow);
        return new Attempt
        {
            Id = id ?? Guid.NewGuid(),
            AssignmentId = assignmentId,
            OpeningId = openingId,
            SnapshotId = snapshotId,
            CandidateEmail = candidateEmail,
            Status = AttemptStatus.InProgress,
            StartedAtUtc = now,
            DueAtUtc = now.AddMinutes(overallDurationMinutes),
            PromptOrderJson = promptOrder,
            DisplaySeed = displaySeed,
            DisplayOrderJson = displayOrder ?? JsonDocument.Parse("{}"),
            RowVersion = 1
        };
    }

    public bool IsDue(IClock clock) => EffectiveNow(clock) >= DueAtUtc;

    /// <summary>
    /// While paused, the clock is frozen at <see cref="PausedAtUtc"/> so lazy expiry cannot fire.
    /// </summary>
    public DateTimeOffset EffectiveNow(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return PausedAtUtc ?? clock.UtcNow;
    }

    public DateTimeOffset Pause(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureInProgress();
        if (PausedAtUtc is not null)
        {
            throw new ConcurrencyException("Session is not running.");
        }

        PausedAtUtc = TruncateToMicroseconds(clock.UtcNow);
        RowVersion++;
        return PausedAtUtc.Value;
    }

    public int Resume(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureInProgress();
        if (PausedAtUtc is null)
        {
            throw new ConcurrencyException("Session is not paused.");
        }

        var now = TruncateToMicroseconds(clock.UtcNow);
        var delta = now - PausedAtUtc.Value;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        var seconds = (int)Math.Round(delta.TotalSeconds, MidpointRounding.AwayFromZero);
        DueAtUtc = DueAtUtc.AddSeconds(seconds);
        PausedAtUtc = null;
        RowVersion++;
        return seconds;
    }

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        var utcTicks = value.UtcTicks - (value.UtcTicks % TimeSpan.TicksPerMicrosecond);
        return new DateTimeOffset(utcTicks, TimeSpan.Zero);
    }

    public int RemainingSeconds(IClock clock)
    {
        var remaining = DueAtUtc - EffectiveNow(clock);
        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Floor(remaining.TotalSeconds);
    }

    public void UpsertAnswers(
        IReadOnlyCollection<(Guid QuestionId, JsonDocument Value)> answers,
        IReadOnlySet<Guid> knownQuestionIds)
    {
        EnsureInProgress();
        foreach (var (questionId, value) in answers)
        {
            if (!knownQuestionIds.Contains(questionId))
            {
                throw new DomainException(UnknownQuestionMessage);
            }

            var existing = _answers.FirstOrDefault(a => a.QuestionId == questionId);
            if (existing is null)
            {
                _answers.Add(AttemptAnswer.Create(Id, questionId, value));
            }
            else
            {
                existing.ReplaceValue(value);
            }
        }
    }

    public void SetFlags(IReadOnlyCollection<(Guid QuestionId, bool Flagged)> flags, IReadOnlySet<Guid> knownQuestionIds)
    {
        EnsureInProgress();
        var changed = false;
        foreach (var (questionId, flagged) in flags)
        {
            if (!knownQuestionIds.Contains(questionId))
            {
                throw new DomainException(UnknownQuestionMessage);
            }

            var existing = _answers.FirstOrDefault(a => a.QuestionId == questionId);
            if (existing is null)
            {
                if (!flagged)
                {
                    continue;
                }

                _answers.Add(AttemptAnswer.FlagOnly(Id, questionId));
                changed = true;
            }
            else if (existing.Flagged != flagged)
            {
                existing.SetFlagged(flagged);
                changed = true;
            }
        }

        if (changed)
        {
            RowVersion++;
        }
    }

    public void Submit(
        IReadOnlyList<AttemptItemResult> itemResults,
        ResultStatus resultStatus,
        decimal autoPointsAwarded,
        decimal autoPointsAvailable,
        decimal totalPointsAvailable,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureInProgress();

        _itemResults.Clear();
        _itemResults.AddRange(itemResults);
        ResultStatus = resultStatus;
        AutoPointsAwarded = autoPointsAwarded;
        AutoPointsAvailable = autoPointsAvailable;
        TotalPointsAvailable = totalPointsAvailable;
        FinalPointsAwarded = resultStatus == global::InterviewQuiz.Evaluation.Domain.ResultStatus.Complete
            ? autoPointsAwarded
            : null;
        Status = AttemptStatus.Submitted;
        SubmittedAtUtc = TruncateToMicroseconds(clock.UtcNow);
        RowVersion++;
    }

    /// <summary>
    /// Records a human score for one <c>humanOnly</c> or <c>aiAssist</c> item.
    /// Returns true only when this call is what settles the last unsettled item.
    /// </summary>
    public bool ApplyHumanScore(
        Guid questionId,
        decimal pointsAwarded,
        string reviewerUserId,
        string? comment,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (!IsSubmitted)
        {
            throw new ConcurrencyException(NotSubmittedMessage);
        }

        var reviewer = reviewerUserId?.Trim() ?? "";
        if (reviewer.Length == 0)
        {
            throw new DomainException(ReviewerRequiredMessage);
        }

        var item = _itemResults.FirstOrDefault(result => result.QuestionId == questionId)
            ?? throw new DomainException(UnknownQuestionMessage);
        if (!item.IsHumanReviewable)
        {
            throw new DomainException(NotReviewableMessage);
        }

        var awarded = decimal.Round(pointsAwarded, 4, MidpointRounding.AwayFromZero);
        if (awarded < 0 || awarded > item.Points)
        {
            throw new DomainException(PointsOutOfRangeMessage);
        }

        var note = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (note is { Length: > ReviewCommentMaxLength })
        {
            throw new DomainException(CommentTooLongMessage);
        }

        var wasComplete = ResultStatus == global::InterviewQuiz.Evaluation.Domain.ResultStatus.Complete;
        item.Settle(awarded, reviewer, TruncateToMicroseconds(clock.UtcNow), note);
        var complete = _itemResults.All(result => result.Status == ItemScoreStatus.Scored);
        if (complete)
        {
            ResultStatus = global::InterviewQuiz.Evaluation.Domain.ResultStatus.Complete;
            FinalPointsAwarded = _itemResults.Sum(result => result.PointsAwarded ?? 0m);
        }
        else
        {
            ResultStatus = global::InterviewQuiz.Evaluation.Domain.ResultStatus.Incomplete;
            FinalPointsAwarded = null;
        }

        RowVersion++;
        return complete && !wasComplete;
    }

    /// <summary>
    /// Stores the latest AI draft on an <c>aiAssist</c> item. Does not settle the item
    /// and does not change a score the reviewer already confirmed.
    /// </summary>
    public void RecordSuggestion(
        Guid questionId,
        decimal suggestedPoints,
        string? feedback,
        string suggestedByUserId,
        string model,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (!IsSubmitted)
        {
            throw new ConcurrencyException(NotSubmittedMessage);
        }

        var actor = suggestedByUserId?.Trim() ?? "";
        if (actor.Length == 0)
        {
            throw new DomainException(ReviewerRequiredMessage);
        }

        var modelName = model?.Trim() ?? "";
        if (modelName.Length == 0 || modelName.Length > SuggestionModelMaxLength)
        {
            throw new DomainException(SuggestionFailedMessage);
        }

        var item = _itemResults.FirstOrDefault(result => result.QuestionId == questionId)
            ?? throw new DomainException(UnknownQuestionMessage);
        if (!item.IsAiAssist)
        {
            throw new DomainException(SuggestionOnlyForAiAssistMessage);
        }

        var points = decimal.Round(suggestedPoints, 4, MidpointRounding.AwayFromZero);
        if (points < 0 || points > item.Points)
        {
            throw new DomainException(PointsOutOfRangeMessage);
        }

        var note = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        if (note is { Length: > SuggestionFeedbackMaxLength })
        {
            throw new DomainException(SuggestionFeedbackTooLongMessage);
        }

        item.RecordSuggestion(points, note, actor, TruncateToMicroseconds(clock.UtcNow), modelName);
        RowVersion++;
    }

    private void EnsureInProgress()
    {
        if (IsSubmitted)
        {
            throw new ConcurrencyException(AlreadySubmittedMessage);
        }
    }
}
