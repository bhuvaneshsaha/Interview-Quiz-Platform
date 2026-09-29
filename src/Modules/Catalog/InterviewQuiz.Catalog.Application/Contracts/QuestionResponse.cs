using System.Text.Json;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class QuestionResponse
{
    public required Guid Id { get; init; }
    public required int SortOrder { get; init; }
    public required QuestionType Type { get; init; }
    public required string Stem { get; init; }
    public required ScoringMode ScoringMode { get; init; }
    public CreditMode? CreditMode { get; init; }
    public required int Points { get; init; }
    public required JsonElement Body { get; init; }
}
