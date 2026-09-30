using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Catalog.Application;

public interface ITemplateService
{
    Task<PublishTemplateResponse> PublishAsync(
        Guid quizId,
        string publishedByUserId,
        CancellationToken cancellationToken);

    Task<PagedResult<TemplateSummaryResponse>> ListAsync(
        TemplateListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<TemplateResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<TemplateVersionSummaryResponse>> ListVersionsAsync(
        Guid templateId,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<TemplateVersionResponse> GetVersionAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken);

    Task<QuizResponse> CloneAsync(
        Guid templateId,
        Guid versionId,
        CloneTemplateVersionRequest request,
        CancellationToken cancellationToken);
}
