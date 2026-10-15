namespace InterviewQuiz.Evaluation.Application.Contracts;

public sealed class CandidateAttemptResponse
{
    public Guid Id { get; init; }
    public Guid AssignmentId { get; init; }
    public string Status { get; init; } = "";
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset DueAtUtc { get; init; }
    public DateTimeOffset? SubmittedAtUtc { get; init; }
    public int RemainingSeconds { get; init; }
    public IReadOnlyList<CandidateQuestionDto> Questions { get; init; } = [];
    public IReadOnlyList<AnswerDto> Answers { get; init; } = [];
    public IReadOnlyList<CandidateItemResultDto>? ItemResults { get; init; }
    public string? LiveState { get; init; }
    public bool Paused { get; init; }
    public string? LiveEndReason { get; init; }
    public IReadOnlyList<Guid> FlaggedQuestionIds { get; init; } = [];
}
