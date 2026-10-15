using System.Text.Json;

namespace InterviewQuiz.Evaluation.Domain;

public sealed class AttemptAnswer
{
    private AttemptAnswer()
    {
        Value = JsonDocument.Parse("{}");
    }

    public Guid AttemptId { get; private set; }
    public Guid QuestionId { get; private set; }
    public JsonDocument Value { get; private set; }
    public bool Flagged { get; private set; }

    public bool HasValue => Value.RootElement.ValueKind != JsonValueKind.Null;

    public static AttemptAnswer Create(Guid attemptId, Guid questionId, JsonDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AttemptAnswer
        {
            AttemptId = attemptId,
            QuestionId = questionId,
            Value = value
        };
    }

    public static AttemptAnswer FlagOnly(Guid attemptId, Guid questionId)
        => new()
        {
            AttemptId = attemptId,
            QuestionId = questionId,
            Value = JsonDocument.Parse("null"),
            Flagged = true
        };

    public void ReplaceValue(JsonDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public void SetFlagged(bool flagged) => Flagged = flagged;
}
