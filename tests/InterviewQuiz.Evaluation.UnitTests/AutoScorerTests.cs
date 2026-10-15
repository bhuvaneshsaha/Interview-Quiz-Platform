using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Evaluation.Application.Scoring;
using InterviewQuiz.Evaluation.Domain;

namespace InterviewQuiz.Evaluation.UnitTests;

public sealed class AutoScorerTests
{
    [Fact]
    public void Mc_single_all_or_nothing()
    {
        var question = McSingle();
        var correct = AutoScorer.ScoreQuestion(question, Json("""{ "optionId": "opt-201" }"""));
        var wrong = AutoScorer.ScoreQuestion(question, Json("""{ "optionId": "opt-200" }"""));
        Assert.Equal(ItemScoreStatus.Scored, correct.Status);
        Assert.Equal(1m, correct.PointsAwarded);
        Assert.Equal(0m, wrong.PointsAwarded);
    }

    [Fact]
    public void True_false_all_or_nothing()
    {
        var question = Tf();
        Assert.Equal(1m, AutoScorer.ScoreQuestion(question, Json("""{ "value": true }""")).PointsAwarded);
        Assert.Equal(0m, AutoScorer.ScoreQuestion(question, Json("""{ "value": false }""")).PointsAwarded);
    }

    [Fact]
    public void Short_text_trims_and_honors_case()
    {
        var insensitive = ShortText(caseSensitive: false);
        var sensitive = ShortText(caseSensitive: true);
        Assert.Equal(1m, AutoScorer.ScoreQuestion(insensitive, Json("""{ "text": "  Rest  " }""")).PointsAwarded);
        Assert.Equal(0m, AutoScorer.ScoreQuestion(sensitive, Json("""{ "text": "rest" }""")).PointsAwarded);
        Assert.Equal(1m, AutoScorer.ScoreQuestion(sensitive, Json("""{ "text": "REST" }""")).PointsAwarded);
    }

    [Fact]
    public void Ordering_partial_uses_adjacent_pairs()
    {
        var question = Ordering(CreditMode.Partial, points: 3);
        var full = AutoScorer.ScoreQuestion(question, Json("""{ "itemIds": ["a","b","c"] }"""));
        var onePair = AutoScorer.ScoreQuestion(question, Json("""{ "itemIds": ["a","b","x"] }"""));
        var shifted = AutoScorer.ScoreQuestion(question, Json("""{ "itemIds": ["a","c","b"] }"""));
        Assert.Equal(3m, full.PointsAwarded);
        Assert.Equal(0m, onePair.PointsAwarded);
        Assert.Equal(0m, shifted.PointsAwarded);

        var mid = AutoScorer.ScoreQuestion(
            Ordering(CreditMode.Partial, points: 2, items: 3),
            Json("""{ "itemIds": ["a","b","c"] }"""));
        Assert.Equal(2m, mid.PointsAwarded);

        var firstPairOnly = AutoScorer.ScoreQuestion(
            Ordering(CreditMode.Partial, points: 2),
            Json("""{ "itemIds": ["a","b","c"] }"""));
        Assert.Equal(2m, firstPairOnly.PointsAwarded);
    }

    [Fact]
    public void Ordering_adjacent_pair_partial_credit()
    {
        var question = new QuizSnapshotQuestionDto(
            Guid.NewGuid(),
            0,
            QuestionType.Ordering,
            "Order",
            ScoringMode.Auto,
            CreditMode.Partial,
            3,
            JsonSerializer.SerializeToElement(new
            {
                items = new[]
                {
                    new { id = "a", text = "A", correctIndex = 0 },
                    new { id = "b", text = "B", correctIndex = 1 },
                    new { id = "c", text = "C", correctIndex = 2 }
                }
            }, CatalogJson.SerializerOptions),
            null);

        var firstPair = AutoScorer.ScoreQuestion(question, Json("""{ "itemIds": ["a","b","c"] }"""));
        Assert.Equal(3m, firstPair.PointsAwarded);

        var onlyFirstAdjacent = AutoScorer.ScoreQuestion(question, Json("""{ "itemIds": ["a","b","x"] }"""));
        Assert.Equal(0m, onlyFirstAdjacent.PointsAwarded);

        var aThenBThenWrongSet = AutoScorer.ScoreQuestion(
            question,
            Json("""{ "itemIds": ["a","c","b"] }"""));
        Assert.Equal(0m, aThenBThenWrongSet.PointsAwarded);
    }

    [Fact]
    public void Human_only_is_unsettled()
    {
        var question = new QuizSnapshotQuestionDto(
            Guid.NewGuid(),
            0,
            QuestionType.LongText,
            "Essay",
            ScoringMode.HumanOnly,
            null,
            5,
            JsonSerializer.SerializeToElement(new { maxLength = 400 }, CatalogJson.SerializerOptions),
            null);

        var score = AutoScorer.ScoreQuestion(question, Json("""{ "text": "hello" }"""));
        Assert.Equal(ItemScoreStatus.Unsettled, score.Status);
        Assert.Null(score.PointsAwarded);
    }

    [Fact]
    public void Mc_multi_partial_is_proportion_of_key()
    {
        var question = new QuizSnapshotQuestionDto(
            Guid.NewGuid(),
            0,
            QuestionType.MultipleChoiceMulti,
            "Pick",
            ScoringMode.Auto,
            CreditMode.Partial,
            2,
            JsonSerializer.SerializeToElement(new
            {
                options = new[]
                {
                    new { id = "a", text = "A", isCorrect = true },
                    new { id = "b", text = "B", isCorrect = true },
                    new { id = "c", text = "C", isCorrect = false }
                }
            }, CatalogJson.SerializerOptions),
            null);

        var half = AutoScorer.ScoreQuestion(question, Json("""{ "optionIds": ["a"] }"""));
        Assert.Equal(1m, half.PointsAwarded);
        var exact = AutoScorer.ScoreQuestion(question, Json("""{ "optionIds": ["a","b"] }"""));
        Assert.Equal(2m, exact.PointsAwarded);
    }

    private static QuizSnapshotQuestionDto McSingle()
        => new(
            Guid.NewGuid(),
            0,
            QuestionType.MultipleChoiceSingle,
            "Status",
            ScoringMode.Auto,
            null,
            1,
            JsonSerializer.SerializeToElement(new
            {
                options = new[]
                {
                    new { id = "opt-200", text = "200", isCorrect = false },
                    new { id = "opt-201", text = "201", isCorrect = true }
                }
            }, CatalogJson.SerializerOptions),
            null);

    private static QuizSnapshotQuestionDto Tf()
        => new(
            Guid.NewGuid(),
            0,
            QuestionType.TrueFalse,
            "TF",
            ScoringMode.Auto,
            null,
            1,
            JsonSerializer.SerializeToElement(new { correct = true }, CatalogJson.SerializerOptions),
            null);

    private static QuizSnapshotQuestionDto ShortText(bool caseSensitive)
        => new(
            Guid.NewGuid(),
            0,
            QuestionType.ShortText,
            "REST",
            ScoringMode.Auto,
            null,
            1,
            JsonSerializer.SerializeToElement(new
            {
                acceptableAnswers = new[] { "REST" },
                caseSensitive
            }, CatalogJson.SerializerOptions),
            null);

    private static QuizSnapshotQuestionDto Ordering(CreditMode credit, int points, int items = 3)
        => new(
            Guid.NewGuid(),
            0,
            QuestionType.Ordering,
            "Order",
            ScoringMode.Auto,
            credit,
            points,
            JsonSerializer.SerializeToElement(new
            {
                items = new[]
                {
                    new { id = "a", text = "A", correctIndex = 0 },
                    new { id = "b", text = "B", correctIndex = 1 },
                    new { id = "c", text = "C", correctIndex = 2 }
                }
            }, CatalogJson.SerializerOptions),
            null);

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();
}
