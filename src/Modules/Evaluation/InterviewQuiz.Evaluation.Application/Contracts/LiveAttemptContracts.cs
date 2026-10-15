namespace InterviewQuiz.Evaluation.Application.Contracts;

public sealed record AttemptLiveFacts(
    Guid AttemptId,
    bool Submitted,
    bool Paused,
    int? RemainingSeconds,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? DueAtUtc,
    int AnsweredCount,
    int FlaggedCount);

public sealed record ReviewFlagUpdate(Guid QuestionId, bool Flagged);

public sealed class SetReviewFlagsRequest
{
    public List<ReviewFlagDto> Flags { get; set; } = [];
}

public sealed class ReviewFlagDto
{
    public Guid QuestionId { get; set; }
    public bool Flagged { get; set; }
}
