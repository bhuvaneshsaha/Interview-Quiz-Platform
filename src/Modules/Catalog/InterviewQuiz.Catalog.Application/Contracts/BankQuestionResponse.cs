using System.Text.Json;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class BankQuestionResponse
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyDictionary<string, string> Tags { get; init; }
    public required int ExpectedExperienceYears { get; init; }
    public required QuestionType Type { get; init; }
    public required string Stem { get; init; }
    public required ScoringMode ScoringMode { get; init; }
    public CreditMode? CreditMode { get; init; }
    public required int Points { get; init; }
    public required JsonElement Body { get; init; }
    public DateTimeOffset? ArchivedAtUtc { get; init; }
    public required uint RowVersion { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
