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

    public void ReplaceValue(JsonDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }
}
