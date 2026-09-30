using System.Text.Json;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Application.Contracts;

/// <summary>
/// Immutable quiz graph (questions, keys, scoring) for Delivery to copy at assign time.
/// </summary>
public sealed record QuizSnapshotDto(
    Guid QuizId,
    Guid OpeningId,
    string Title,
    string Description,
    int ExpectedExperienceYears,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<QuizSnapshotQuestionDto> Questions,
    uint RowVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record QuizSnapshotQuestionDto(
    Guid Id,
    int SortOrder,
    QuestionType Type,
    string Stem,
    ScoringMode ScoringMode,
    CreditMode? CreditMode,
    int Points,
    JsonElement Body,
    Guid? SourceQuestionId);
