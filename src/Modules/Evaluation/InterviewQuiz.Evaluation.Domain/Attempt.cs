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

    private readonly List<AttemptAnswer> _answers = [];
    private readonly List<AttemptItemResult> _itemResults = [];

    private Attempt()
    {
        PromptOrderJson = JsonDocument.Parse("{}");
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
    public JsonDocument PromptOrderJson { get; private set; }
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
        Guid? id = null)
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
        Status = AttemptStatus.Submitted;
        SubmittedAtUtc = clock.UtcNow;
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
