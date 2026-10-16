namespace InterviewQuiz.Evaluation.Application.Contracts;

public sealed class CandidateSubmitResponse
{
    public Guid Id { get; init; }
    public Guid AssignmentId { get; init; }
    public string Status { get; init; } = "";
    public string ResultStatus { get; init; } = "";
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset DueAtUtc { get; init; }
    public DateTimeOffset? SubmittedAtUtc { get; init; }

    /// <summary>Omitted when the assignment hides the score from the candidate.</summary>
    public decimal? AutoPointsAwarded { get; init; }

    /// <summary>Omitted when the assignment hides the score from the candidate.</summary>
    public decimal? AutoPointsAvailable { get; init; }

    /// <summary>Omitted when the assignment hides the score from the candidate.</summary>
    public decimal? TotalPointsAvailable { get; init; }

    /// <summary>Omitted when the assignment hides per-item correctness from the candidate.</summary>
    public IReadOnlyList<CandidateItemResultDto>? ItemResults { get; init; }
}
