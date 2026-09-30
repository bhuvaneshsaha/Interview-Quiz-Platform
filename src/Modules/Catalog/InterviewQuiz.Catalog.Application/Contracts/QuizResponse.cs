namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class QuizResponse
{
    public required Guid Id { get; init; }
    public required Guid OpeningId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required int ExpectedExperienceYears { get; init; }
    public required IReadOnlyDictionary<string, string> Tags { get; init; }
    public required IReadOnlyList<QuestionResponse> Questions { get; init; }
    public Guid? OriginTemplateId { get; init; }
    public Guid? SourceTemplateVersionId { get; init; }
    public required uint RowVersion { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
