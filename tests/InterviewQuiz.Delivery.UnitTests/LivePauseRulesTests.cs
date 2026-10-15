using InterviewQuiz.Delivery.Domain;

namespace InterviewQuiz.Delivery.UnitTests;

public sealed class LivePauseRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Exceeded_when_one_pause_passes_fifteen_minutes()
    {
        Assert.False(LivePauseRules.Exceeded(Start, 0, Start.AddMinutes(15)));
        Assert.True(LivePauseRules.Exceeded(Start, 0, Start.AddMinutes(15).AddSeconds(1)));
    }

    [Fact]
    public void Exceeded_when_total_pause_passes_thirty_minutes()
    {
        var pausedAt = Start.AddMinutes(20);
        Assert.False(LivePauseRules.Exceeded(pausedAt, 20 * 60, pausedAt.AddMinutes(10)));
        Assert.True(LivePauseRules.Exceeded(pausedAt, 20 * 60, pausedAt.AddMinutes(10).AddSeconds(1)));
    }
}
