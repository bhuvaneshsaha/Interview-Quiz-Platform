using System.Net.Mail;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;

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
    public const string LiveStartUnavailableMessage = "Assignment is live; start is not available.";
    public const string InviteLiveMessage = "Invite links are for async assignments.";
    public const string InviteNoLongerInvitableMessage = "Assignment is no longer invitable.";

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

    public bool IsTerminal => Status is AssignmentStatus.Submitted
        or AssignmentStatus.PendingReview
        or AssignmentStatus.Completed;

    public bool IsInvitable => Mode == AssignmentMode.Async && !IsTerminal;

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
            CreatedByUserId = createdByUserId.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            RowVersion = 1
        };

        return assignment;
    }

    public void MarkAttemptStarted(IClock clock)
    {
        if (IsTerminal)
        {
            throw new ConcurrencyException("Assignment is no longer in progress.");
        }

        if (Status == AssignmentStatus.InProgress)
        {
            return;
        }

        Status = AssignmentStatus.InProgress;
        Touch(clock);
    }

    public void MarkAttemptSubmitted(string resultStatus, IClock clock)
    {
        if (string.Equals(resultStatus, "complete", StringComparison.OrdinalIgnoreCase))
        {
            Status = AssignmentStatus.Completed;
        }
        else if (string.Equals(resultStatus, "incomplete", StringComparison.OrdinalIgnoreCase))
        {
            Status = AssignmentStatus.PendingReview;
        }
        else
        {
            throw new DomainException("Result status is invalid.");
        }

        Touch(clock);
    }

    public void EnsureInvitable()
    {
        if (Mode != AssignmentMode.Async)
        {
            throw new DomainException(InviteLiveMessage);
        }

        if (IsTerminal)
        {
            throw new DomainException(InviteNoLongerInvitableMessage);
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
        if (mode == AssignmentMode.Live)
        {
            if (overallDurationMinutes is null)
            {
                return null;
            }

            if (overallDurationMinutes < MinDurationMinutes || overallDurationMinutes > MaxDurationMinutes)
            {
                throw new DomainException(AsyncDurationRangeMessage);
            }

            return overallDurationMinutes;
        }

        if (overallDurationMinutes is null)
        {
            throw new DomainException(AsyncDurationRequiredMessage);
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
