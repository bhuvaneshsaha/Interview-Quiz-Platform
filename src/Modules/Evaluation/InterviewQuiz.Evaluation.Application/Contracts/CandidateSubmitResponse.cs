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
    public decimal AutoPointsAwarded { get; init; }
    public decimal AutoPointsAvailable { get; init; }
    public decimal TotalPointsAvailable { get; init; }
    public IReadOnlyList<CandidateItemResultDto> ItemResults { get; init; } = [];
}
