namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class PublishTemplateResponse
{
    public required Guid TemplateId { get; init; }
    public required Guid VersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required bool CreatedNewTemplate { get; init; }
}
