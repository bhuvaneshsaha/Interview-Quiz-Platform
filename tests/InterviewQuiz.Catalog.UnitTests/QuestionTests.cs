using System.Text.Json;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Catalog.UnitTests;

public sealed class QuestionTests
{
    [Fact]
    public void MultipleChoiceSingle_happy_path()
    {
        var question = Create(
            QuestionType.MultipleChoiceSingle,
            new
            {
                options = new[]
                {
                    new { id = "a", text = "Alpha", isCorrect = true },
                    new { id = "b", text = "Beta", isCorrect = false }
                }
            });

        Assert.Equal(ScoringMode.Auto, question.ScoringMode);
        Assert.Null(question.CreditMode);
        Assert.Contains("\"isCorrect\":true", question.Body.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleChoiceSingle_auto_rejects_zero_correct()
    {
        var ex = Assert.Throws<DomainException>(() => Create(
            QuestionType.MultipleChoiceSingle,
            new
            {
                options = new[]
                {
                    new { id = "a", text = "Alpha", isCorrect = false },
                    new { id = "b", text = "Beta", isCorrect = false }
                }
            }));
        Assert.Contains("exactly one correct", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultipleChoiceMulti_happy_path()
    {
        var question = Create(
            QuestionType.MultipleChoiceMulti,
            new
            {
                options = new[]
                {
                    new { id = "a", text = "Alpha", isCorrect = true },
                    new { id = "b", text = "Beta", isCorrect = true }
                }
            },
            creditMode: CreditMode.Partial);

        Assert.Equal(CreditMode.Partial, question.CreditMode);
    }

    [Fact]
    public void MultipleChoiceMulti_auto_rejects_zero_correct()
    {
        var ex = Assert.Throws<DomainException>(() => Create(
            QuestionType.MultipleChoiceMulti,
            new
            {
                options = new[]
                {
                    new { id = "a", text = "Alpha", isCorrect = false },
                    new { id = "b", text = "Beta", isCorrect = false }
                }
            },
            creditMode: CreditMode.AllOrNothing));
        Assert.Contains("at least one correct", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrueFalse_happy_path()
    {
        var question = Create(QuestionType.TrueFalse, new { correct = false });
        Assert.Equal(JsonValueKind.False, question.Body.RootElement.GetProperty("correct").ValueKind);
    }

    [Fact]
    public void TrueFalse_rejects_missing_correct()
    {
        var ex = Assert.Throws<DomainException>(() => Create(QuestionType.TrueFalse, new { }));
        Assert.Contains("correct", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShortText_happy_path_defaults_to_auto_when_keyed()
    {
        var question = Create(
            QuestionType.ShortText,
            new { acceptableAnswers = new[] { "  REST  ", "Representational State Transfer" }, caseSensitive = false },
            scoring: null);

        Assert.Equal(ScoringMode.Auto, question.ScoringMode);
        var answers = question.Body.RootElement.GetProperty("acceptableAnswers").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Equal(["REST", "Representational State Transfer"], answers);
    }

    [Fact]
    public void ShortText_rejects_duplicate_answers()
    {
        var ex = Assert.Throws<DomainException>(() => Create(
            QuestionType.ShortText,
            new { acceptableAnswers = new[] { "REST", "REST" } }));
        Assert.Contains("unique", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LongText_happy_path_defaults_to_human_only()
    {
        var question = Create(
            QuestionType.LongText,
            new { maxLength = 400, guidance = "Be specific." },
            scoring: null);

        Assert.Equal(ScoringMode.HumanOnly, question.ScoringMode);
    }

    [Fact]
    public void LongText_rejects_auto_scoring()
    {
        var ex = Assert.Throws<DomainException>(() => Create(
            QuestionType.LongText,
            new { },
            scoring: ScoringMode.Auto));
        Assert.Contains("cannot use auto", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DragDropSharedBank_happy_path()
    {
        var question = Create(
            QuestionType.DragDropSharedBank,
            new
            {
                slots = new[] { new { id = "s1", label = "GET", correctItemId = "read" } },
                bank = new[]
                {
                    new { id = "read", text = "Read", isDistractor = false },
                    new { id = "noise", text = "Noise", isDistractor = true }
                }
            });

        Assert.Equal(ScoringMode.Auto, question.ScoringMode);
    }

    [Fact]
    public void DragDropSharedBank_rejects_distractor_as_correct()
    {
        var ex = Assert.Throws<DomainException>(() => Create(
            QuestionType.DragDropSharedBank,
            new
            {
                slots = new[] { new { id = "s1", label = "GET", correctItemId = "noise" } },
                bank = new[]
                {
                    new { id = "read", text = "Read", isDistractor = false },
                    new { id = "noise", text = "Noise", isDistractor = true }
                }
            }));
        Assert.Contains("non-distractor", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DragDropPerSlot_happy_path()
    {
        var question = Create(
            QuestionType.DragDropPerSlot,
            new
            {
                slots = new[]
                {
                    new
                    {
                        id = "s1",
                        label = "401",
                        options = new[]
                        {
                            new { id = "a", text = "Unauthenticated", isCorrect = true },
                            new { id = "b", text = "Forbidden", isCorrect = false }
                        }
                    }
                }
            });

        Assert.Equal(QuestionType.DragDropPerSlot, question.Type);
    }

    [Fact]
    public void DragDropPerSlot_auto_rejects_wrong_correct_count()
    {
        var ex = Assert.Throws<DomainException>(() => Create(
            QuestionType.DragDropPerSlot,
            new
            {
                slots = new[]
                {
                    new
                    {
                        id = "s1",
                        label = "401",
                        options = new[]
                        {
                            new { id = "a", text = "Unauthenticated", isCorrect = false },
                            new { id = "b", text = "Forbidden", isCorrect = false }
                        }
                    }
                }
            }));
        Assert.Contains("exactly one correct", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ordering_happy_path()
    {
        var question = Create(
            QuestionType.Ordering,
            new
            {
                items = new[]
                {
                    new { id = "a", text = "First", correctIndex = 0 },
                    new { id = "b", text = "Second", correctIndex = 1 }
                }
            },
            creditMode: CreditMode.Partial);

        Assert.Equal(CreditMode.Partial, question.CreditMode);
    }

    [Fact]
    public void Ordering_rejects_duplicate_indexes()
    {
        var ex = Assert.Throws<DomainException>(() => Create(
            QuestionType.Ordering,
            new
            {
                items = new[]
                {
                    new { id = "a", text = "First", correctIndex = 0 },
                    new { id = "b", text = "Second", correctIndex = 0 }
                }
            },
            creditMode: CreditMode.AllOrNothing));
        Assert.Contains("0..n-1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Credit_mode_required_for_multi_and_omitted_for_single()
    {
        var missing = Assert.Throws<DomainException>(() => Create(
            QuestionType.MultipleChoiceMulti,
            new
            {
                options = new[]
                {
                    new { id = "a", text = "A", isCorrect = true },
                    new { id = "b", text = "B", isCorrect = false }
                }
            }));
        Assert.Contains("Credit mode is required", missing.Message, StringComparison.OrdinalIgnoreCase);

        var extra = Assert.Throws<DomainException>(() => Create(
            QuestionType.TrueFalse,
            new { correct = true },
            creditMode: CreditMode.Partial));
        Assert.Contains("must be omitted", extra.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_empty_stem_and_non_positive_points()
    {
        var stem = Assert.Throws<DomainException>(() => Create(
            QuestionType.TrueFalse,
            new { correct = true },
            stem: "  "));
        Assert.Contains("Stem", stem.Message, StringComparison.OrdinalIgnoreCase);

        var points = Assert.Throws<DomainException>(() => Question.Create(
            null,
            0,
            QuestionType.TrueFalse,
            "Stem",
            ScoringMode.Auto,
            null,
            0,
            JsonSerializer.SerializeToElement(new { correct = true }, CatalogJson.SerializerOptions)));
        Assert.Contains("Points", points.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShortText_without_answers_defaults_to_human_only()
    {
        var question = Create(
            QuestionType.ShortText,
            new { acceptableAnswers = Array.Empty<string>(), caseSensitive = false },
            scoring: null);

        Assert.Equal(ScoringMode.HumanOnly, question.ScoringMode);
    }

    [Fact]
    public void Quiz_allows_empty_question_list_and_rejects_long_title()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        var quiz = Quiz.Create(
            Guid.NewGuid(),
            "Working copy",
            null,
            3,
            new Dictionary<string, string> { ["Role"] = "Backend" },
            [],
            clock);

        Assert.Empty(quiz.Questions);

        var ex = Assert.Throws<DomainException>(() => Quiz.Create(
            Guid.NewGuid(),
            new string('x', Quiz.TitleMaxLength + 1),
            null,
            1,
            null,
            [],
            clock));
        Assert.Contains("Title", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Code_question_type_is_rejected_by_json_converter()
    {
        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<QuestionType>("\"code\"", CatalogJson.SerializerOptions));
        Assert.Contains("Code question types", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Question Create(
        QuestionType type,
        object body,
        ScoringMode? scoring = ScoringMode.Auto,
        CreditMode? creditMode = null,
        string stem = "A readable stem")
        => Question.Create(
            null,
            0,
            type,
            stem,
            scoring,
            creditMode,
            1,
            JsonSerializer.SerializeToElement(body, CatalogJson.SerializerOptions));

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }
}
