using System.Text.Json;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Delivery.UnitTests;

public sealed class AssignmentTransitionTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(AssignmentStatus.NotStarted, AssignmentStatus.InProgress, true)]
    [InlineData(AssignmentStatus.InProgress, AssignmentStatus.InProgress, true)]
    [InlineData(AssignmentStatus.NotStarted, AssignmentStatus.PendingReview, true)]
    [InlineData(AssignmentStatus.InProgress, AssignmentStatus.PendingReview, true)]
    [InlineData(AssignmentStatus.NotStarted, AssignmentStatus.Completed, true)]
    [InlineData(AssignmentStatus.InProgress, AssignmentStatus.Completed, true)]
    [InlineData(AssignmentStatus.PendingReview, AssignmentStatus.Completed, false)]
    [InlineData(AssignmentStatus.Completed, AssignmentStatus.PendingReview, false)]
    [InlineData(AssignmentStatus.PendingReview, AssignmentStatus.InProgress, false)]
    [InlineData(AssignmentStatus.Completed, AssignmentStatus.InProgress, false)]
    [InlineData(AssignmentStatus.NotStarted, AssignmentStatus.NotStarted, false)]
    public void CanTransition_matches_the_explicit_table(
        AssignmentStatus from,
        AssignmentStatus to,
        bool allowed)
    {
        Assert.Equal(allowed, Assignment.CanTransition(from, to));
    }

    [Fact]
    public void Start_moves_notStarted_to_inProgress_and_a_second_start_is_a_no_op()
    {
        var assignment = Create();

        assignment.MarkAttemptStarted(_clock);
        Assert.Equal(AssignmentStatus.InProgress, assignment.Status);
        var version = assignment.RowVersion;

        assignment.MarkAttemptStarted(_clock);
        Assert.Equal(AssignmentStatus.InProgress, assignment.Status);
        Assert.Equal(version, assignment.RowVersion);
    }

    [Fact]
    public void Submit_complete_from_notStarted_moves_to_completed()
    {
        var assignment = Create();

        assignment.MarkAttemptSubmitted("complete", _clock);

        Assert.Equal(AssignmentStatus.Completed, assignment.Status);
        Assert.True(assignment.IsTerminal);
    }

    [Fact]
    public void Submit_incomplete_from_inProgress_moves_to_pendingReview()
    {
        var assignment = Create();
        assignment.MarkAttemptStarted(_clock);

        assignment.MarkAttemptSubmitted("incomplete", _clock);

        Assert.Equal(AssignmentStatus.PendingReview, assignment.Status);
        Assert.True(assignment.IsTerminal);
    }

    [Fact]
    public void Submit_from_a_terminal_status_is_rejected()
    {
        var assignment = Create();
        assignment.MarkAttemptSubmitted("complete", _clock);

        var ex = Assert.Throws<ConcurrencyException>(() => assignment.MarkAttemptSubmitted("incomplete", _clock));

        Assert.Equal(
            Assignment.TransitionDeniedMessage(AssignmentStatus.Completed, AssignmentStatus.PendingReview),
            ex.Message);
        Assert.Equal(AssignmentStatus.Completed, assignment.Status);
    }

    [Fact]
    public void Start_from_pendingReview_is_rejected()
    {
        var assignment = Create();
        assignment.MarkAttemptSubmitted("incomplete", _clock);

        var ex = Assert.Throws<ConcurrencyException>(() => assignment.MarkAttemptStarted(_clock));

        Assert.Contains("pendingReview", ex.Message, StringComparison.Ordinal);
        Assert.Contains("inProgress", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_result_status_is_rejected()
    {
        var assignment = Create();

        var ex = Assert.Throws<DomainException>(() => assignment.MarkAttemptSubmitted("submitted", _clock));

        Assert.Equal(Assignment.InvalidResultStatusMessage, ex.Message);
        Assert.Equal(AssignmentStatus.NotStarted, assignment.Status);
    }

    private Assignment Create()
    {
        var id = Guid.NewGuid();
        var snapshot = AssignmentSnapshot.Freeze(
            Guid.NewGuid(),
            id,
            "Quiz",
            1,
            JsonDocument.Parse("{}"),
            _clock.UtcNow,
            quizRowVersion: 1);
        return Assignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "candidate@example.com",
            AssignmentMode.Async,
            30,
            1,
            "recruiter",
            snapshot,
            _clock,
            id);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }
    }
}
