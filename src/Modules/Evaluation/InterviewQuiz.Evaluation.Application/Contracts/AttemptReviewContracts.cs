using System.Text.Json;

namespace InterviewQuiz.Evaluation.Application.Contracts;

public sealed class ReviewItemRequest
{
    public decimal PointsAwarded { get; set; }

    public string? Comment { get; set; }
}

public sealed class AttemptReviewItemDto
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

    /// <summary>Redacted snapshot body so the reviewer can label the candidate's answer.</summary>
    public JsonElement? Body { get; init; }

    /// <summary>Snapshot body including answer keys. Present only on review endpoints.</summary>
    public JsonElement? AnswerKey { get; init; }

    public bool Reviewable { get; init; }
    public string? ReviewedByUserId { get; init; }
    public DateTimeOffset? ReviewedAtUtc { get; init; }
    public string? ReviewComment { get; init; }
    public decimal? SuggestedPoints { get; init; }
    public string? SuggestionFeedback { get; init; }
    public string? SuggestedByUserId { get; init; }
    public DateTimeOffset? SuggestedAtUtc { get; init; }
    public string? SuggestionModel { get; init; }
}

public sealed class AttemptReviewResponse
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
    public decimal? FinalPointsAwarded { get; init; }
    public bool AiSuggestionsEnabled { get; init; }
    public long QuizRowVersion { get; init; }
    public IReadOnlyList<AttemptReviewItemDto> Items { get; init; } = [];
}
