using System.Text.Json;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Application.Contracts;

/// <summary>
/// Candidate-facing quiz body. No points and no answer keys.
/// </summary>
public sealed class CandidateQuizPreviewResponse
{
    public required string Title { get; init; }
    public required string OpeningName { get; init; }
    public required int QuestionCount { get; init; }
    public required int SuggestedDurationMinutes { get; init; }
    public required uint RowVersion { get; init; }
    public required IReadOnlyList<CandidatePreviewQuestionDto> Questions { get; init; }
}

public sealed class CandidatePreviewQuestionDto
{
    public required Guid Id { get; init; }
    public required int SortOrder { get; init; }
    public required QuestionType Type { get; init; }
    public required string Stem { get; init; }
    public required JsonElement Body { get; init; }
    public Guid? SourceQuestionId { get; init; }
}
