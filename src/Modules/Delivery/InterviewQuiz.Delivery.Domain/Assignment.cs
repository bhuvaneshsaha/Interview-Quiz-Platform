using System.Net.Mail;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Mail;

namespace InterviewQuiz.Delivery.Domain;

public sealed class Assignment
{
    public const int EmailMaxLength = 256;
    public const int MinDurationMinutes = 1;
    public const int MaxDurationMinutes = 480;
    public const int MinAttemptLimit = 1;
    public const int MaxAttemptLimit = 20;
    public const int DefaultAttemptLimit = 1;

    public const string OpeningMissingMessage = "Opening does not exist.";
    public const string QuizMissingMessage = "Quiz does not exist.";
    public const string QuizOpeningMismatchMessage = "Quiz does not belong to that opening.";
    public const string QuizHasNoQuestionsMessage = "Quiz has no questions.";
    public const string AsyncDurationRequiredMessage = "Overall duration is required for async assignments.";
    public const string AsyncDurationRangeMessage = "Overall duration must be between 1 and 480 minutes.";
    public const string LiveStartUnavailableMessage = LiveSessionMessages.WaitingForHost;
    public const string LiveDurationRequiredMessage = "Overall duration is required for live assignments.";
    public const string InviteLiveMessage = "Invite links are for async assignments.";
    public const string InviteNoLongerInvitableMessage = "Assignment is no longer invitable.";
    public const int InviteEmailErrorMaxLength = 500;

    public const string InviteEmailNone = "none";
    public const string InviteEmailSent = "sent";
    public const string InviteEmailFailed = "failed";
    public const string InviteEmailResent = "resent";

    private Assignment()
    {
    }

    public Guid Id { get; private set; }
    public Guid OpeningId { get; private set; }
    public Guid QuizId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public AssignmentSnapshot Snapshot { get; private set; } = null!;
    public string CandidateEmail { get; private set; } = "";
    public AssignmentMode Mode { get; private set; }
    public int? OverallDurationMinutes { get; private set; }
    public int AttemptLimit { get; private set; }
    public AssignmentStatus Status { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public int RowVersion { get; private set; }

    public string InviteEmailStatus { get; private set; } = InviteEmailNone;
    public int InviteEmailAttemptCount { get; private set; }
    public DateTimeOffset? InviteEmailLastAttemptAt { get; private set; }
    public string? InviteEmailLastError { get; private set; }

    public LiveSessionState? LiveState { get; private set; }
    public DateTimeOffset? LiveStartedAtUtc { get; private set; }
    public DateTimeOffset? PausedAtUtc { get; private set; }
    public int TotalPausedSeconds { get; private set; }
    public DateTimeOffset? LiveEndedAtUtc { get; private set; }
    public LiveEndReason? SessionEndReason { get; private set; }
    public DateTimeOffset? CandidateLastSeenAtUtc { get; private set; }
    public int? CurrentQuestionIndex { get; private set; }
    public DateTimeOffset? LastActivityAtUtc { get; private set; }

    public const string InvalidResultStatusMessage = "Result status is invalid.";

    public bool IsTerminal => Status is AssignmentStatus.PendingReview or AssignmentStatus.Completed;

    /// <summary>
    /// Legal moves: notStarted → inProgress; notStarted|inProgress → pendingReview (incomplete)
    /// or completed (complete); pendingReview → completed when human review settles the attempt.
    /// Start while inProgress is a no-op. completed does not move. pendingReview stays closed
    /// for invites and new starts.
    /// </summary>
    public static bool CanTransition(AssignmentStatus from, AssignmentStatus to)
        => (from, to) switch
        {
            (AssignmentStatus.NotStarted, AssignmentStatus.InProgress) => true,
            (AssignmentStatus.InProgress, AssignmentStatus.InProgress) => true,
            (AssignmentStatus.NotStarted, AssignmentStatus.PendingReview) => true,
            (AssignmentStatus.InProgress, AssignmentStatus.PendingReview) => true,
            (AssignmentStatus.NotStarted, AssignmentStatus.Completed) => true,
            (AssignmentStatus.InProgress, AssignmentStatus.Completed) => true,
            (AssignmentStatus.PendingReview, AssignmentStatus.Completed) => true,
            _ => false
        };

    public static string TransitionDeniedMessage(AssignmentStatus from, AssignmentStatus to)
        => $"Assignment cannot move from {ToWire(from)} to {ToWire(to)}.";

    public static string ToWire(AssignmentStatus status)
        => status switch
        {
            AssignmentStatus.NotStarted => "notStarted",
            AssignmentStatus.InProgress => "inProgress",
            AssignmentStatus.PendingReview => "pendingReview",
            AssignmentStatus.Completed => "completed",
            _ => throw new DomainException("Assignment status is invalid.")
        };

    public bool IsInvitable => Mode is AssignmentMode.Async or AssignmentMode.Live && !IsTerminal;

    public LiveSessionState EffectiveLiveState => LiveState ?? LiveSessionState.Created;

    public static Assignment Create(
        Guid openingId,
        Guid quizId,
        string candidateEmail,
        AssignmentMode mode,
        int? overallDurationMinutes,
        int? attemptLimit,
        string createdByUserId,
        AssignmentSnapshot snapshot,
        IClock clock,
        Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(clock);

        if (openingId == Guid.Empty)
        {
            throw new DomainException(OpeningMissingMessage);
        }

        if (quizId == Guid.Empty)
        {
            throw new DomainException(QuizMissingMessage);
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new DomainException("Created by user is required.");
        }

        var now = clock.UtcNow;
        var assignmentId = id ?? Guid.NewGuid();
        if (snapshot.AssignmentId != assignmentId || snapshot.Id == Guid.Empty)
        {
            throw new DomainException("Snapshot does not belong to the assignment.");
        }

        var assignment = new Assignment
        {
            Id = assignmentId,
            OpeningId = openingId,
            QuizId = quizId,
            SnapshotId = snapshot.Id,
            Snapshot = snapshot,
            CandidateEmail = NormalizeEmail(candidateEmail),
            Mode = mode,
            OverallDurationMinutes = NormalizeDuration(mode, overallDurationMinutes),
            AttemptLimit = NormalizeAttemptLimit(attemptLimit),
            Status = AssignmentStatus.NotStarted,
            LiveState = mode == AssignmentMode.Live ? LiveSessionState.Created : null,
            CreatedByUserId = createdByUserId.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            RowVersion = 1
        };

        return assignment;
    }

    public void MarkAttemptStarted(IClock clock)
    {
        if (Status == AssignmentStatus.InProgress)
        {
            return;
        }

        EnsureTransition(AssignmentStatus.InProgress);
        Status = AssignmentStatus.InProgress;
        Touch(clock);
    }

    public void MarkAttemptSubmitted(string resultStatus, IClock clock)
    {
        var target = resultStatus?.Trim().ToLowerInvariant() switch
        {
            "complete" => AssignmentStatus.Completed,
            "incomplete" => AssignmentStatus.PendingReview,
            _ => throw new DomainException(InvalidResultStatusMessage)
        };

        EnsureTransition(target);
        Status = target;
        Touch(clock);
    }

    /// <summary>
    /// Human review finished the last unsettled item. Already completed is a no-op.
    /// </summary>
    public void CompleteReview(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (Status == AssignmentStatus.Completed)
        {
            return;
        }

        if (Status != AssignmentStatus.PendingReview)
        {
            throw new ConcurrencyException(TransitionDeniedMessage(Status, AssignmentStatus.Completed));
        }

        Status = AssignmentStatus.Completed;
        Touch(clock);
    }

    private void EnsureTransition(AssignmentStatus to)
    {
        if (!CanTransition(Status, to))
        {
            throw new ConcurrencyException(TransitionDeniedMessage(Status, to));
        }
    }

    public void RecordInviteDelivery(bool succeeded, string? error, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        InviteEmailAttemptCount++;
        InviteEmailLastAttemptAt = clock.UtcNow;
        if (!succeeded)
        {
            InviteEmailStatus = InviteEmailFailed;
            var redacted = InviteSecretRedactor.Redact(error, InviteEmailErrorMaxLength);
            InviteEmailLastError = string.IsNullOrWhiteSpace(redacted) ? "Email could not be sent." : redacted;
        }
        else
        {
            InviteEmailStatus = InviteEmailAttemptCount <= 1 ? InviteEmailSent : InviteEmailResent;
            InviteEmailLastError = null;
        }

        Touch(clock);
    }

    public void EnsureInvitable()
    {
        if (Mode is not (AssignmentMode.Async or AssignmentMode.Live))
        {
            throw new DomainException(InviteLiveMessage);
        }

        if (IsTerminal)
        {
            throw new DomainException(InviteNoLongerInvitableMessage);
        }
    }

    /// <summary>
    /// First consume moves Created to Lobby and returns true so the caller can audit once.
    /// Later consumes and heartbeats only refresh last seen.
    /// </summary>
    public bool NoteCandidateJoined(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (Mode != AssignmentMode.Live)
        {
            return false;
        }

        var first = EffectiveLiveState == LiveSessionState.Created;
        if (first)
        {
            LiveState = LiveSessionState.Lobby;
        }

        NoteCandidateSeen(clock);
        return first;
    }

    public void NoteCandidateSeen(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (Mode != AssignmentMode.Live)
        {
            return;
        }

        var now = clock.UtcNow;
        CandidateLastSeenAtUtc = now;
        if (EffectiveLiveState is not (LiveSessionState.Ended or LiveSessionState.Submitted))
        {
            LastActivityAtUtc = now;
        }

        Touch(clock);
    }

    public void MarkLiveRunning(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureLive();
        if (EffectiveLiveState is LiveSessionState.Running or LiveSessionState.Paused)
        {
            throw new ConcurrencyException(LiveSessionMessages.AlreadyStarted);
        }

        if (EffectiveLiveState is LiveSessionState.Ended or LiveSessionState.Submitted)
        {
            throw new ConcurrencyException(LiveSessionMessages.Ended);
        }

        if (EffectiveLiveState != LiveSessionState.Lobby || !LivePauseRules.JoinedRecently(CandidateLastSeenAtUtc, clock.UtcNow))
        {
            throw new ConcurrencyException(LiveSessionMessages.CandidateNotJoined);
        }

        LiveState = LiveSessionState.Running;
        LiveStartedAtUtc = clock.UtcNow;
        Touch(clock);
    }

    public void MarkLivePaused(DateTimeOffset pausedAtUtc, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureLive();
        if (EffectiveLiveState != LiveSessionState.Running)
        {
            throw new ConcurrencyException(LiveSessionMessages.NotRunning);
        }

        LiveState = LiveSessionState.Paused;
        PausedAtUtc = pausedAtUtc;
        Touch(clock);
    }

    public void MarkLiveResumed(int pausedSeconds, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureLive();
        if (EffectiveLiveState != LiveSessionState.Paused)
        {
            throw new ConcurrencyException(LiveSessionMessages.NotPaused);
        }

        if (pausedSeconds < 0)
        {
            pausedSeconds = 0;
        }

        TotalPausedSeconds += pausedSeconds;
        PausedAtUtc = null;
        LiveState = LiveSessionState.Running;
        Touch(clock);
    }

    /// <summary>
    /// Returns false when the session was already terminal so the caller does not audit twice.
    /// </summary>
    public bool MarkLiveEnded(LiveEndReason reason, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureLive();
        if (EffectiveLiveState is LiveSessionState.Ended or LiveSessionState.Submitted)
        {
            return false;
        }

        LiveState = LiveSessionState.Ended;
        SessionEndReason = reason;
        LiveEndedAtUtc = clock.UtcNow;
        PausedAtUtc = null;
        Touch(clock);
        return true;
    }

    /// <summary>
    /// Attempt submit. A host, timeout, or pause-limit end keeps its reason and does not audit again.
    /// </summary>
    public bool NoteLiveAttemptSubmitted(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (Mode != AssignmentMode.Live)
        {
            return false;
        }

        if (EffectiveLiveState == LiveSessionState.Submitted)
        {
            return false;
        }

        if (EffectiveLiveState == LiveSessionState.Ended)
        {
            LiveState = LiveSessionState.Submitted;
            Touch(clock);
            return false;
        }

        LiveState = LiveSessionState.Submitted;
        SessionEndReason = LiveEndReason.Submitted;
        LiveEndedAtUtc = clock.UtcNow;
        PausedAtUtc = null;
        Touch(clock);
        return true;
    }

    public void SetCurrentQuestion(int questionIndex, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureLive();
        if (EffectiveLiveState is not (LiveSessionState.Running or LiveSessionState.Paused))
        {
            throw new ConcurrencyException(LiveSessionMessages.NotRunning);
        }

        var count = Snapshot?.QuestionCount ?? 0;
        if (questionIndex < 0 || questionIndex >= count)
        {
            throw new DomainException(LiveSessionMessages.QuestionIndexInvalid);
        }

        CurrentQuestionIndex = questionIndex;
        var now = clock.UtcNow;
        CandidateLastSeenAtUtc = now;
        LastActivityAtUtc = now;
        Touch(clock);
    }

    public bool PauseLimitExceeded(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (Mode != AssignmentMode.Live || EffectiveLiveState != LiveSessionState.Paused || PausedAtUtc is null)
        {
            return false;
        }

        return LivePauseRules.Exceeded(PausedAtUtc.Value, TotalPausedSeconds, clock.UtcNow);
    }

    private void EnsureLive()
    {
        if (Mode != AssignmentMode.Live)
        {
            throw new DomainException(LiveSessionMessages.NotLive);
        }
    }

    public void EnsureAsyncStartAvailable()
    {
        if (Mode != AssignmentMode.Async)
        {
            throw new DomainException(LiveStartUnavailableMessage);
        }
    }

    public static AssignmentMode ParseMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            throw new DomainException("Mode is required.");
        }

        if (string.Equals(mode.Trim(), "async", StringComparison.OrdinalIgnoreCase))
        {
            return AssignmentMode.Async;
        }

        if (string.Equals(mode.Trim(), "live", StringComparison.OrdinalIgnoreCase))
        {
            return AssignmentMode.Live;
        }

        throw new DomainException("Mode is invalid.");
    }

    public static string NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Candidate email is required.");
        }

        var trimmed = email.Trim().ToLowerInvariant();
        if (trimmed.Length > EmailMaxLength)
        {
            throw new DomainException("Candidate email is invalid.");
        }

        try
        {
            var parsed = new MailAddress(trimmed);
            if (!parsed.Address.Contains('.', StringComparison.Ordinal))
            {
                throw new DomainException("Candidate email is invalid.");
            }

            return parsed.Address;
        }
        catch (FormatException)
        {
            throw new DomainException("Candidate email is invalid.");
        }
    }

    public static int NormalizeAttemptLimit(int? attemptLimit)
    {
        var value = attemptLimit ?? DefaultAttemptLimit;
        if (value < MinAttemptLimit || value > MaxAttemptLimit)
        {
            throw new DomainException("Attempt limit is invalid.");
        }

        return value;
    }

    public static int? NormalizeDuration(AssignmentMode mode, int? overallDurationMinutes)
    {
        if (overallDurationMinutes is null)
        {
            throw new DomainException(
                mode == AssignmentMode.Live ? LiveDurationRequiredMessage : AsyncDurationRequiredMessage);
        }

        if (overallDurationMinutes < MinDurationMinutes || overallDurationMinutes > MaxDurationMinutes)
        {
            throw new DomainException(AsyncDurationRangeMessage);
        }

        return overallDurationMinutes;
    }

    private void Touch(IClock clock)
    {
        UpdatedAtUtc = clock.UtcNow;
        RowVersion++;
    }
}
