namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class TemplateSummaryResponse
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required int ExpectedExperienceYears { get; init; }
    public required IReadOnlyDictionary<string, string> Tags { get; init; }
    public required Guid LatestVersionId { get; init; }
    public required int LatestVersionNumber { get; init; }
    public required Guid OriginQuizId { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class TemplateResponse
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required int ExpectedExperienceYears { get; init; }
    public required IReadOnlyDictionary<string, string> Tags { get; init; }
    public required Guid LatestVersionId { get; init; }
    public required int LatestVersionNumber { get; init; }
    public required Guid OriginQuizId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
