using System.Text.Json;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Evaluation.UnitTests;

public sealed class AttemptReviewTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Human_score_settles_human_and_ai_items_and_sets_the_final_total()
    {
        var attempt = SubmittedMixed();
        var human = attempt.ItemResults.Single(item => item.ScoringMode == "humanOnly");
        var assist = attempt.ItemResults.Single(item => item.ScoringMode == "aiAssist");

        var first = attempt.ApplyHumanScore(human.QuestionId, 3.33333m, " reviewer ", "  note  ", _clock);

        Assert.False(first);
        Assert.Equal(ResultStatus.Incomplete, attempt.ResultStatus);
        Assert.Null(attempt.FinalPointsAwarded);
        var settled = attempt.ItemResults.Single(item => item.QuestionId == human.QuestionId);
        Assert.Equal(ItemScoreStatus.Scored, settled.Status);
        Assert.Equal(3.3333m, settled.PointsAwarded);
        Assert.Equal("reviewer", settled.ReviewedByUserId);
        Assert.Equal("note", settled.ReviewComment);

        var last = attempt.ApplyHumanScore(assist.QuestionId, 4m, "reviewer", null, _clock);

        Assert.True(last);
        Assert.Equal(ResultStatus.Complete, attempt.ResultStatus);
        Assert.Equal(8.3333m, attempt.FinalPointsAwarded);

        var again = attempt.ApplyHumanScore(assist.QuestionId, 5m, "reviewer-2", "", _clock);
        Assert.False(again);
        Assert.Equal(9.3333m, attempt.FinalPointsAwarded);
        Assert.Null(attempt.ItemResults.Single(item => item.QuestionId == assist.QuestionId).ReviewComment);
    }

    [Fact]
    public void Auto_items_out_of_range_points_and_an_open_attempt_are_rejected()
    {
        var attempt = SubmittedMixed();
        var auto = attempt.ItemResults.Single(item => item.ScoringMode == "auto");
        var human = attempt.ItemResults.Single(item => item.ScoringMode == "humanOnly");

        var autoEx = Assert.Throws<DomainException>(() =>
            attempt.ApplyHumanScore(auto.QuestionId, 1m, "reviewer", null, _clock));
        Assert.Equal(Attempt.NotReviewableMessage, autoEx.Message);

        var rangeEx = Assert.Throws<DomainException>(() =>
            attempt.ApplyHumanScore(human.QuestionId, 5.0001m, "reviewer", null, _clock));
        Assert.Equal(Attempt.PointsOutOfRangeMessage, rangeEx.Message);

        var open = Attempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "candidate@example.com",
            30,
            JsonDocument.Parse("{}"),
            _clock);
        var openEx = Assert.Throws<ConcurrencyException>(() =>
            open.ApplyHumanScore(human.QuestionId, 1m, "reviewer", null, _clock));
        Assert.Equal(Attempt.NotSubmittedMessage, openEx.Message);
    }

    [Fact]
    public void All_auto_submit_stores_the_auto_total_as_the_final_score()
    {
        var attempt = Attempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "candidate@example.com",
            30,
            JsonDocument.Parse("{}"),
            _clock);
        attempt.Submit(
            [Item(attempt.Id, "auto", 2, ItemScoreStatus.Scored, 2m)],
            ResultStatus.Complete,
            2m,
            2m,
            2m,
            _clock);

        Assert.Equal(2m, attempt.FinalPointsAwarded);
    }

    private Attempt SubmittedMixed()
    {
        var attempt = Attempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "candidate@example.com",
            30,
            JsonDocument.Parse("{}"),
            _clock);
        attempt.Submit(
            [
                Item(attempt.Id, "auto", 1, ItemScoreStatus.Scored, 1m),
                Item(attempt.Id, "humanOnly", 5, ItemScoreStatus.Unsettled, null),
                Item(attempt.Id, "aiAssist", 5, ItemScoreStatus.Unsettled, null)
            ],
            ResultStatus.Incomplete,
            1m,
            1m,
            11m,
            _clock);
        return attempt;
    }

    private static AttemptItemResult Item(
        Guid attemptId,
        string scoringMode,
        int points,
        ItemScoreStatus status,
        decimal? awarded)
        => AttemptItemResult.Create(attemptId, Guid.NewGuid(), 0, "longText", scoringMode, points, status, awarded);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
