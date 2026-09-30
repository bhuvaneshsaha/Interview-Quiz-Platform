using System.Text.Json;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.UnitTests;

public sealed class BankQuestionTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_rejects_invalid_multiple_choice_body()
    {
        var ex = Assert.Throws<DomainException>(() => BankQuestion.Create(
            "HTTP codes",
            null,
            3,
            QuestionType.MultipleChoiceSingle,
            "Pick one",
            ScoringMode.Auto,
            null,
            1,
            JsonSerializer.SerializeToElement(new
            {
                options = new[]
                {
                    new { id = "a", text = "One", isCorrect = false },
                    new { id = "b", text = "Two", isCorrect = false }
                }
            }, CatalogJson.SerializerOptions),
            _clock));

        Assert.Contains("exactly one correct", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_accepts_true_false_payload()
    {
        var item = BankQuestion.Create(
            "One opening",
            new Dictionary<string, string> { ["Role"] = "Backend" },
            0,
            QuestionType.TrueFalse,
            "A quiz belongs to one opening.",
            ScoringMode.Auto,
            null,
            1,
            JsonSerializer.SerializeToElement(new { correct = true }, CatalogJson.SerializerOptions),
            _clock,
            Guid.Parse("6d0f4a43-9e5a-4f2d-ab44-3c1f5e9d4002"));

        Assert.Equal("One opening", item.Title);
        Assert.Equal(QuestionType.TrueFalse, item.Type);
        Assert.Null(item.ArchivedAtUtc);
        Assert.False(item.IsArchived);
    }

    [Fact]
    public void Archive_then_unarchive()
    {
        var item = LiveTrueFalse();
        item.Archive(_clock);
        Assert.Equal(_clock.UtcNow, item.ArchivedAtUtc);
        Assert.Equal(1u, item.RowVersion);

        item.Unarchive(_clock);
        Assert.Null(item.ArchivedAtUtc);
        Assert.Equal(2u, item.RowVersion);
    }

    [Fact]
    public void Archive_already_archived_is_rejected()
    {
        var item = LiveTrueFalse();
        item.Archive(_clock);
        var ex = Assert.Throws<DomainException>(() => item.Archive(_clock));
        Assert.Equal("Question is already archived.", ex.Message);
    }

    [Fact]
    public void Unarchive_not_archived_is_rejected()
    {
        var item = LiveTrueFalse();
        var ex = Assert.Throws<DomainException>(() => item.Unarchive(_clock));
        Assert.Equal("Question is not archived.", ex.Message);
    }

    [Fact]
    public void Create_rejects_experience_above_80()
    {
        var ex = Assert.Throws<DomainException>(() => BankQuestion.Create(
            "Too senior",
            null,
            81,
            QuestionType.TrueFalse,
            "Stem",
            ScoringMode.Auto,
            null,
            1,
            JsonSerializer.SerializeToElement(new { correct = true }, CatalogJson.SerializerOptions),
            _clock));
        Assert.Contains("0 and 80", ex.Message, StringComparison.Ordinal);
    }

    private BankQuestion LiveTrueFalse()
        => BankQuestion.Create(
            "One opening",
            null,
            0,
            QuestionType.TrueFalse,
            "A quiz belongs to one opening.",
            ScoringMode.Auto,
            null,
            1,
            JsonSerializer.SerializeToElement(new { correct = true }, CatalogJson.SerializerOptions),
            _clock);

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }
}
