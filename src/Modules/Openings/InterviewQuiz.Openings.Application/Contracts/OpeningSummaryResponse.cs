namespace InterviewQuiz.Openings.Application.Contracts;

public sealed class OpeningCandidateCounts
{
    public required int Assigned { get; init; }
    public required int Appeared { get; init; }
    public required int Submitted { get; init; }
}

public sealed class OpeningQuizSummary
{
    public required Guid Id { get; init; }
    public string? Title { get; init; }
    public int? QuestionCount { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public OpeningCandidateCounts? Candidates { get; init; }
}

public sealed class OpeningSummaryResponse
{
    public required Guid OpeningId { get; init; }
    public IReadOnlyList<OpeningQuizSummary>? Quizzes { get; init; }
    public OpeningCandidateCounts? Candidates { get; init; }
}
