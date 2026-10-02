namespace InterviewQuiz.Evaluation.Application.Contracts;

public sealed class CandidateItemResultDto
{
    public Guid QuestionId { get; init; }
    public string ScoringMode { get; init; } = "";
    public string Status { get; init; } = "";
    public decimal? PointsAwarded { get; init; }
}
