using System.Text.Json;

namespace InterviewQuiz.Evaluation.Application.Contracts;

public class AttemptSummaryResponse
{
    public Guid Id { get; init; }
    public Guid AssignmentId { get; init; }
    public Guid OpeningId { get; init; }
    public string CandidateEmail { get; init; } = "";
    public string Status { get; init; } = "";
    public string ResultStatus { get; init; } = "";
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? SubmittedAtUtc { get; init; }
    public decimal AutoPointsAwarded { get; init; }
    public decimal AutoPointsAvailable { get; init; }
    public decimal TotalPointsAvailable { get; init; }
}

public sealed class AttemptResultItemDto
{
    public Guid QuestionId { get; init; }
    public int SortOrder { get; init; }
    public string Type { get; init; } = "";
    public string ScoringMode { get; init; } = "";
    public int Points { get; init; }
    public string Status { get; init; } = "";
    public decimal? PointsAwarded { get; init; }
    public JsonElement? CandidateAnswer { get; init; }
    public string? Stem { get; init; }
}

public sealed class AttemptResultResponse : AttemptSummaryResponse
{
    public IReadOnlyList<AttemptResultItemDto> Items { get; init; } = [];
}
