using System.Text.Json;
using InterviewQuiz.Catalog.Application.CandidatePreview;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Evaluation.Application.Scoring;

namespace InterviewQuiz.Evaluation.UnitTests;

public sealed class AttemptDisplayOrderTests
{
    private static readonly Guid First = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid Second = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
    private static readonly Guid Third = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3");
    private static readonly Guid Fourth = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4");
    private static readonly Guid Ordering = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5");
    private static readonly Guid Shared = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6");

    [Fact]
    public void Same_seed_keeps_one_order_and_other_seeds_usually_differ()
    {
        var snapshot = Snapshot(shuffleQuestions: true, shuffleOptions: true);
        var first = AttemptDisplayOrder.Create(snapshot, 42);
        var again = AttemptDisplayOrder.Create(snapshot, 42);
        Assert.Equal(42, first.Seed);
        Assert.Equal(first.Order.RootElement.GetRawText(), again.Order.RootElement.GetRawText());

        var seen = new HashSet<string> { first.Order.RootElement.GetRawText() };
        for (var seed = 1; seed <= 24; seed++)
        {
            seen.Add(AttemptDisplayOrder.Create(snapshot, seed).Order.RootElement.GetRawText());
        }

        Assert.True(seen.Count > 1);
    }

    [Fact]
    public void Shuffle_off_keeps_canonical_order_and_skips_option_maps()
    {
        var snapshot = Snapshot(shuffleQuestions: false, shuffleOptions: false);
        var plan = AttemptDisplayOrder.Create(snapshot, 7);
        Assert.Equal([First, Second, Third, Fourth, Ordering, Shared], AttemptDisplayOrder.ParseQuestionIds(plan.Order));
        Assert.Empty(AttemptDisplayOrder.ParseOptionOrder(plan.Order));
    }

    [Fact]
    public void Option_shuffle_is_multiple_choice_only_and_does_not_restore_answer_keys()
    {
        var snapshot = Snapshot(shuffleQuestions: false, shuffleOptions: true);
        var plan = AttemptDisplayOrder.Create(snapshot, 3);
        var optionOrder = AttemptDisplayOrder.ParseOptionOrder(plan.Order);
        Assert.Equal([First, Second, Third, Fourth], optionOrder.Keys.OrderBy(id => id).ToArray());
        Assert.DoesNotContain(Ordering, optionOrder.Keys);
        Assert.DoesNotContain(Shared, optionOrder.Keys);

        var question = snapshot.Questions.Single(q => q.Id == First);
        var body = CandidateQuestionRedactor.RedactBody(
            question,
            new Dictionary<Guid, IReadOnlyList<string>>(),
            optionOrder);
        var ids = body.GetProperty("options").EnumerateArray()
            .Select(option => option.GetProperty("id").GetString()!)
            .ToArray();
        Assert.Equal(optionOrder[First], ids);
        Assert.Contains(
            optionOrder.Values,
            stored => !stored.SequenceEqual(["a", "b", "c", "d"]));
        Assert.DoesNotContain("isCorrect", body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Stored_question_order_is_what_the_candidate_sees()
    {
        var snapshot = Snapshot(shuffleQuestions: true, shuffleOptions: false);
        var plan = AttemptDisplayOrder.Create(snapshot, 11);
        var ordered = AttemptDisplayOrder.OrderQuestions(snapshot.Questions, plan.Order);
        Assert.Equal(AttemptDisplayOrder.ParseQuestionIds(plan.Order), ordered.Select(q => q.Id).ToArray());
        Assert.Equal(0, ordered.Single(q => q.Id == First).SortOrder);
    }

    [Fact]
    public void Old_assignment_snapshot_deserializes_shuffle_as_off()
    {
        const string json = """
            {
              "quizId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1",
              "openingId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2",
              "title": "Legacy",
              "description": "",
              "expectedExperienceYears": 1,
              "tags": {},
              "questions": [],
              "rowVersion": 1,
              "createdAtUtc": "2026-01-01T00:00:00Z",
              "updatedAtUtc": "2026-01-01T00:00:00Z"
            }
            """;
        var dto = JsonSerializer.Deserialize<QuizSnapshotDto>(json, CatalogJson.SerializerOptions);
        Assert.NotNull(dto);
        Assert.False(dto!.ShuffleQuestions);
        Assert.False(dto.ShuffleOptions);

        var roundTrip = JsonSerializer.Deserialize<QuizSnapshotDto>(
            JsonSerializer.Serialize(dto, CatalogJson.SerializerOptions),
            CatalogJson.SerializerOptions);
        Assert.False(roundTrip!.ShuffleQuestions);
        Assert.False(roundTrip.ShuffleOptions);
    }

    [Fact]
    public void Scoring_uses_option_ids_after_the_display_order_changes()
    {
        var snapshot = Snapshot(shuffleQuestions: true, shuffleOptions: true);
        var plan = AttemptDisplayOrder.Create(snapshot, 5);
        var optionOrder = AttemptDisplayOrder.ParseOptionOrder(plan.Order);
        var correct = optionOrder[First].Single(id => id == "d");
        var answers = new Dictionary<Guid, JsonElement>
        {
            [First] = JsonSerializer.SerializeToElement(new { optionId = correct }, CatalogJson.SerializerOptions)
        };

        var score = AutoScorer.ScoreAttempt(snapshot, answers).Single(item => item.QuestionId == First);
        Assert.Equal(1m, score.PointsAwarded);
        Assert.Equal(0, score.SortOrder);
    }

    private static QuizSnapshotDto Snapshot(bool shuffleQuestions, bool shuffleOptions)
        => new(
            First,
            Second,
            "Shuffle",
            "",
            1,
            new Dictionary<string, string>(),
            [
                Mcq(First, 0),
                Mcq(Second, 1),
                Mcq(Third, 2),
                Mcq(Fourth, 3),
                OrderingQuestion(),
                SharedQuestion()
            ],
            1,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            shuffleQuestions,
            shuffleOptions);

    private static QuizSnapshotQuestionDto Mcq(Guid id, int sortOrder)
        => new(
            id,
            sortOrder,
            QuestionType.MultipleChoiceSingle,
            $"Stem {sortOrder}",
            ScoringMode.Auto,
            null,
            1,
            JsonSerializer.SerializeToElement(
                new
                {
                    options = new object[]
                    {
                        new { id = "a", text = "A", isCorrect = false },
                        new { id = "b", text = "B", isCorrect = false },
                        new { id = "c", text = "C", isCorrect = false },
                        new { id = "d", text = "D", isCorrect = true }
                    }
                },
                CatalogJson.SerializerOptions),
            null);

    private static QuizSnapshotQuestionDto OrderingQuestion()
        => new(
            Ordering,
            4,
            QuestionType.Ordering,
            "Order these.",
            ScoringMode.Auto,
            CreditMode.Partial,
            3,
            JsonSerializer.SerializeToElement(
                new
                {
                    items = new object[]
                    {
                        new { id = "third", text = "Third", correctIndex = 2 },
                        new { id = "first", text = "First", correctIndex = 0 },
                        new { id = "second", text = "Second", correctIndex = 1 }
                    }
                },
                CatalogJson.SerializerOptions),
            null);

    private static QuizSnapshotQuestionDto SharedQuestion()
        => new(
            Shared,
            5,
            QuestionType.DragDropSharedBank,
            "Match each slot.",
            ScoringMode.Auto,
            CreditMode.Partial,
            2,
            JsonSerializer.SerializeToElement(
                new
                {
                    slots = new object[]
                    {
                        new { id = "slot-a", label = "A", correctItemId = "item-a" }
                    },
                    bank = new object[]
                    {
                        new { id = "item-a", text = "A", isDistractor = false }
                    }
                },
                CatalogJson.SerializerOptions),
            null);
}
