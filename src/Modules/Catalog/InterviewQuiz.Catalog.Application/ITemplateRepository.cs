using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Catalog.Application;

public sealed record TemplateListRow(Template Template, TemplateVersion LatestVersion);

public interface ITemplateRepository
{
    Task<Template?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Template?> GetByOriginQuizIdAsync(Guid quizId, CancellationToken cancellationToken);
    Task<TemplateVersion?> GetVersionAsync(Guid templateId, Guid versionId, CancellationToken cancellationToken);
    Task<TemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken cancellationToken);
    Task<int> GetMaxVersionNumberAsync(Guid templateId, CancellationToken cancellationToken);
    Task AddAsync(Template template, CancellationToken cancellationToken);
    Task AddVersionAsync(TemplateVersion version, CancellationToken cancellationToken);
    Task<PagedResult<TemplateListRow>> ListLatestAsync(
        TemplateListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken);
    Task<PagedResult<TemplateVersion>> ListVersionsAsync(
        Guid templateId,
        PageRequest page,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
