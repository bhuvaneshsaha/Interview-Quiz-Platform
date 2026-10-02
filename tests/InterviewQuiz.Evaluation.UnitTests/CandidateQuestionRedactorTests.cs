using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Evaluation.Application.Scoring;

namespace InterviewQuiz.Evaluation.UnitTests;

public sealed class CandidateQuestionRedactorTests
{
    [Fact]
    public void Shared_bank_payload_omits_slot_keys_and_distractor_flags()
    {
        var question = new QuizSnapshotQuestionDto(
            Guid.NewGuid(),
            0,
            QuestionType.DragDropSharedBank,
            "Match the verb",
            ScoringMode.Auto,
            null,
            2,
            JsonSerializer.SerializeToElement(new
            {
                slots = new[]
                {
                    new { id = "slot-get", label = "GET", correctItemId = "item-read" }
                },
                bank = new[]
                {
                    new { id = "item-read", text = "Read a resource", isDistractor = false },
                    new { id = "item-format", text = "Change the encoding", isDistractor = true }
                }
            }, CatalogJson.SerializerOptions),
            null);

        var body = CandidateQuestionRedactor.RedactBody(question, new Dictionary<Guid, IReadOnlyList<string>>());
        var json = body.GetRawText();

        Assert.Contains("Read a resource", json, StringComparison.Ordinal);
        Assert.Contains("Change the encoding", json, StringComparison.Ordinal);
        Assert.Contains("slot-get", json, StringComparison.Ordinal);
        Assert.Contains("item-read", json, StringComparison.Ordinal);
        Assert.DoesNotContain("correctItemId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isDistractor", json, StringComparison.Ordinal);
    }
}
