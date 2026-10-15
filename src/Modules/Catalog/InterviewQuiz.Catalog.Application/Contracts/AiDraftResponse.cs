namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class AiDraftResponse
{
    public required Guid Id { get; init; }
    public required Guid OpeningId { get; init; }
    public required string Title { get; init; }
    public required int ExpectedExperienceYears { get; init; }
    public string? ResumeText { get; init; }
    public string? RulesText { get; init; }
    public Guid? TemplateId { get; init; }
    public Guid? TemplateVersionId { get; init; }
    public required string Model { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<QuestionResponse> Questions { get; init; }
    public Guid? AcceptedQuizId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class AiDraftAcceptResponse
{
    public required Guid DraftId { get; init; }
    public required Guid QuizId { get; init; }
    public required string Title { get; init; }
}
