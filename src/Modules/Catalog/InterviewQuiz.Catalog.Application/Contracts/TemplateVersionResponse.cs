namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class TemplateVersionSummaryResponse
{
    public required Guid Id { get; init; }
    public required Guid TemplateId { get; init; }
    public required int VersionNumber { get; init; }
    public required string Title { get; init; }
    public required Guid PublishedFromQuizId { get; init; }
    public required DateTimeOffset PublishedAtUtc { get; init; }
    public required int QuestionCount { get; init; }
}

public sealed class TemplateVersionResponse
{
    public required Guid Id { get; init; }
    public required Guid TemplateId { get; init; }
    public required int VersionNumber { get; init; }
    public required string Title { get; init; }
    public required Guid PublishedFromQuizId { get; init; }
    public required DateTimeOffset PublishedAtUtc { get; init; }
    public required int QuestionCount { get; init; }
    public required string Description { get; init; }
    public required int ExpectedExperienceYears { get; init; }
    public required IReadOnlyDictionary<string, string> Tags { get; init; }
    public required IReadOnlyList<QuestionResponse> Questions { get; init; }
}
