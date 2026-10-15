using System.Text.Json;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Evaluation.Application.Contracts;

public sealed class CandidateQuestionDto
{
    public Guid Id { get; init; }
    public int SortOrder { get; init; }
    public QuestionType Type { get; init; }
    public string Stem { get; init; } = "";
    public ScoringMode ScoringMode { get; init; }
    public CreditMode? CreditMode { get; init; }
    public int Points { get; init; }
    public JsonElement Body { get; init; }
    public Guid? SourceQuestionId { get; init; }
}
