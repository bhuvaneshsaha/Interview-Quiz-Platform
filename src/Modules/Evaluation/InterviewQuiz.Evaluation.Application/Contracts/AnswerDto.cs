using System.Text.Json;

namespace InterviewQuiz.Evaluation.Application.Contracts;

public sealed class AnswerDto
{
    public Guid QuestionId { get; set; }
    public JsonElement Value { get; set; }
}
