using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence;

public sealed class TemplateRepository : ITemplateRepository
{
    private readonly CatalogDbContext _db;

    public TemplateRepository(CatalogDbContext db)
    {
        _db = db;
    }

    public Task<Template?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.Templates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Template?> GetByOriginQuizIdAsync(Guid quizId, CancellationToken cancellationToken)
        => _db.Templates.FirstOrDefaultAsync(t => t.OriginQuizId == quizId, cancellationToken);

    public Task<TemplateVersion?> GetVersionAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
        => _db.TemplateVersions
            .Include(v => v.Questions)
            .FirstOrDefaultAsync(
                v => v.TemplateId == templateId && v.Id == versionId,
                cancellationToken);

    public Task<TemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken cancellationToken)
        => _db.TemplateVersions
            .Include(v => v.Questions)
            .Where(v => v.TemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> GetMaxVersionNumberAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var max = await _db.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(cancellationToken);
        return max ?? 0;
    }

    public async Task AddAsync(Template template, CancellationToken cancellationToken)
        => await _db.Templates.AddAsync(template, cancellationToken);

    public async Task AddVersionAsync(TemplateVersion version, CancellationToken cancellationToken)
        => await _db.TemplateVersions.AddAsync(version, cancellationToken);

    public async Task<PagedResult<TemplateListRow>> ListLatestAsync(
        TemplateListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var versions = _db.TemplateVersions.AsNoTracking();
        var latestNumbers = versions
            .GroupBy(v => v.TemplateId)
            .Select(g => new { TemplateId = g.Key, VersionNumber = g.Max(v => v.VersionNumber) });

        var query =
            from template in _db.Templates.AsNoTracking()
            join version in versions on template.Id equals version.TemplateId
            join latest in latestNumbers
                on new { version.TemplateId, version.VersionNumber }
                equals new { latest.TemplateId, latest.VersionNumber }
            select new { Template = template, LatestVersion = version };

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword.Trim().ToLower();
            query = query.Where(row => row.LatestVersion.Title.ToLower().Contains(keyword));
        }

        if (criteria.ExperienceMinYears is { } minYears)
        {
            query = query.Where(row => row.LatestVersion.ExpectedExperienceYears >= minYears);
        }

        if (criteria.ExperienceMaxYears is { } maxYears)
        {
            query = query.Where(row => row.LatestVersion.ExpectedExperienceYears <= maxYears);
        }

        if (criteria.Tags is { Count: > 0 })
        {
            foreach (var (key, value) in criteria.Tags)
            {
                var fragment = System.Text.Json.JsonSerializer.Serialize(
                    new Dictionary<string, string> { [key] = value });
                query = query.Where(row => EF.Functions.JsonContains(row.LatestVersion.Tags, fragment));
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(row => row.Template.UpdatedAtUtc)
            .ThenBy(row => row.Template.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var rows = items
            .Select(item => new TemplateListRow(item.Template, item.LatestVersion))
            .ToList();

        return new PagedResult<TemplateListRow>(rows, page.Page, page.PageSize, total);
    }

    public async Task<PagedResult<TemplateVersion>> ListVersionsAsync(
        Guid templateId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = _db.TemplateVersions
            .AsNoTracking()
            .Include(v => v.Questions)
            .Where(v => v.TemplateId == templateId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(v => v.VersionNumber)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TemplateVersion>(items, page.Page, page.PageSize, total);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Template was modified by another request. Reload and retry.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConcurrencyException("Template version was published by another request. Reload and retry.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException postgres
           && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
