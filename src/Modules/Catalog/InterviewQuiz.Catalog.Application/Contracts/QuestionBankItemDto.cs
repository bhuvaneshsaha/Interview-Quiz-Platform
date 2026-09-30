using System.Text.Json;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Application.Contracts;

/// <summary>
/// Question-bank item shape for slice 4. Library fields are unused until bank CRUD ships.
/// </summary>
public sealed class QuestionBankItemDto
{
    public required Guid Id { get; init; }
    public required int SortOrder { get; init; }
    public required QuestionType Type { get; init; }
    public required string Stem { get; init; }
    public required ScoringMode ScoringMode { get; init; }
    public CreditMode? CreditMode { get; init; }
    public required int Points { get; init; }
    public required JsonElement Body { get; init; }
    public Guid? SourceQuestionId { get; init; }
    public string? Title { get; init; }
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
    public int? ExpectedExperienceYears { get; init; }
}
