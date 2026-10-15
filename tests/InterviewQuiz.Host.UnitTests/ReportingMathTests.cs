using InterviewQuiz.Host.Composition;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Host.UnitTests;

public sealed class ReportingMathTests
{
    [Fact]
    public void Funnel_rates_are_zero_when_nobody_is_assigned()
    {
        var counts = ReportingMath.Funnel([], new Dictionary<Guid, ReportingMath.FunnelAttempt>());
        Assert.Equal(0, counts.Assigned);
        Assert.Equal(0m, counts.AppearedRate);
        Assert.Equal(0m, counts.SubmittedRate);
        Assert.Equal(0m, counts.CompletedRate);
    }

    [Fact]
    public void Funnel_counts_distinct_emails_and_branch_statuses()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var counts = ReportingMath.Funnel(
            [
                new(first, "a@example.com", "completed"),
                new(second, "a@example.com", "notStarted"),
                new(third, "b@example.com", "pendingReview")
            ],
            new Dictionary<Guid, ReportingMath.FunnelAttempt>
            {
                [first] = new(true, true),
                [third] = new(true, true)
            });

        Assert.Equal(2, counts.Assigned);
        Assert.Equal(2, counts.Appeared);
        Assert.Equal(2, counts.Submitted);
        Assert.Equal(1, counts.Completed);
        Assert.Equal(1, counts.PendingReview);
        Assert.Equal(1m, counts.AppearedRate);
        Assert.Equal(1m, counts.SubmittedRate);
        Assert.Equal(0.5m, counts.CompletedRate);
    }

    [Fact]
    public void Pass_uses_auto_ratio_until_a_final_score_exists()
    {
        Assert.True(ReportingMath.Passed(7m, 10m));
        Assert.False(ReportingMath.Passed(6.9m, 10m));
        Assert.Null(ReportingMath.Passed(0m, 0m));
        Assert.Equal(0.5m, ReportingMath.Rate(1, 2));

        Assert.True(ReportingMath.Passed(10m, 10m));
        Assert.False(ReportingMath.Passed(10m, 10m, finalPointsAwarded: 10m, totalPointsAvailable: 20m));
        Assert.True(ReportingMath.Passed(10m, 10m, finalPointsAwarded: 14m, totalPointsAvailable: 20m));
        Assert.True(ReportingMath.Passed(0m, 0m, finalPointsAwarded: 7m, totalPointsAvailable: 10m));
        Assert.Null(ReportingMath.Passed(0m, 0m, finalPointsAwarded: 0m, totalPointsAvailable: 0m));
    }

    [Fact]
    public void Async_time_ignores_pause_and_live_time_subtracts_pause_once()
    {
        var started = new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
        var submitted = started.AddSeconds(150);

        var asyncTaken = ReportingMath.Elapsed(live: false, started, submitted, totalPausedSeconds: 120);
        Assert.Equal(150, asyncTaken.DurationSeconds);
        Assert.Null(asyncTaken.Note);

        var liveTaken = ReportingMath.Elapsed(live: true, started, submitted, totalPausedSeconds: 120);
        Assert.Equal(30, liveTaken.DurationSeconds);
        Assert.True(liveTaken.PauseTracked);

        var clamped = ReportingMath.Elapsed(live: true, started, submitted, totalPausedSeconds: 500);
        Assert.Equal(0, clamped.DurationSeconds);
    }

    [Fact]
    public void Discrimination_is_hidden_below_thirty_and_is_the_quartile_delta()
    {
        var small = Enumerable.Range(0, 29)
            .Select(index => (AutoScore: (decimal)index, FullPoints: index % 2 == 0))
            .ToArray();
        var hidden = ReportingMath.QuestionStats(small);
        Assert.Null(hidden.Discrimination);

        var samples = new List<(decimal AutoScore, bool FullPoints)>();
        for (var index = 0; index < 30; index++)
        {
            var full = index >= 23;
            samples.Add((index, full));
        }

        var stats = ReportingMath.QuestionStats(samples);
        Assert.Equal(7, 30 / 4);
        Assert.Equal(1m, stats.Discrimination);
        Assert.Equal(0.2333m, stats.Difficulty);
    }

    [Fact]
    public void Csv_is_utf8_bom_quoted_and_capped_at_10000()
    {
        var bytes = ReportCsv.Write(["openingTitle", "note"], [["Senior, backend", "say \"hello\""]]);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        var text = System.Text.Encoding.UTF8.GetString(bytes.AsSpan(3));
        Assert.Equal("openingTitle,note\n\"Senior, backend\",\"say \"\"hello\"\"\"\n", text);

        var ex = Assert.Throws<DomainException>(() => ReportCsv.EnsureWithinCap(10_001));
        Assert.Equal(ReportCsv.OverCapMessage, ex.Message);
        ReportCsv.EnsureWithinCap(10_000);
    }
}
