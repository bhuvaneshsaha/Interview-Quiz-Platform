using System.Text.Json;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Clock;

namespace InterviewQuiz.Evaluation.UnitTests;

public sealed class AttemptClockTests
{
    [Fact]
    public void Pause_freezes_due_and_resume_moves_it_by_the_paused_seconds()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        var attempt = Attempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "candidate@example.com",
            30,
            JsonDocument.Parse("{}"),
            clock);
        var due = attempt.DueAtUtc;

        attempt.Pause(clock);
        clock.Advance(TimeSpan.FromSeconds(120));
        Assert.False(attempt.IsDue(clock));
        Assert.Equal(due - attempt.PausedAtUtc!.Value, TimeSpan.FromSeconds(attempt.RemainingSeconds(clock)));

        var seconds = attempt.Resume(clock);
        Assert.Equal(120, seconds);
        Assert.Equal(due.AddSeconds(120), attempt.DueAtUtc);
        Assert.False(attempt.IsPaused);
    }

    private sealed class ManualClock : IClock
    {
        public ManualClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }
}
