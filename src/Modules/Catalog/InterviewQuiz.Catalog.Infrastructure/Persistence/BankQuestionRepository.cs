using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence;

public sealed class BankQuestionRepository : IBankQuestionRepository
{
    private readonly CatalogDbContext _db;

    public BankQuestionRepository(CatalogDbContext db)
    {
        _db = db;
    }

    public Task<BankQuestion?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.BankQuestions.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task AddAsync(BankQuestion question, CancellationToken cancellationToken)
        => await _db.BankQuestions.AddAsync(question, cancellationToken);

    public void SetExpectedRowVersion(BankQuestion question, uint expectedRowVersion)
        => _db.Entry(question).Property(q => q.RowVersion).OriginalValue = expectedRowVersion;

    public async Task<PagedResult<BankQuestion>> ListAsync(
        BankQuestionListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(_db.BankQuestions.AsNoTracking(), criteria);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(q => q.UpdatedAtUtc)
            .ThenBy(q => q.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BankQuestion>(items, page.Page, page.PageSize, total);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Question was modified by another request. Reload and retry.");
        }
    }

    private static IQueryable<BankQuestion> ApplyFilters(
        IQueryable<BankQuestion> query,
        BankQuestionListCriteria criteria)
    {
        query = criteria.Archived
            ? query.Where(q => q.ArchivedAtUtc != null)
            : query.Where(q => q.ArchivedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword.Trim().ToLower();
            query = query.Where(q =>
                q.Title.ToLower().Contains(keyword) || q.Stem.ToLower().Contains(keyword));
        }

        if (criteria.Type is { } type)
        {
            query = query.Where(q => q.Type == type);
        }

        if (criteria.ExperienceMinYears is { } minYears)
        {
            query = query.Where(q => q.ExpectedExperienceYears >= minYears);
        }

        if (criteria.ExperienceMaxYears is { } maxYears)
        {
            query = query.Where(q => q.ExpectedExperienceYears <= maxYears);
        }

        if (criteria.Tags is { Count: > 0 })
        {
            foreach (var (key, value) in criteria.Tags)
            {
                var fragment = System.Text.Json.JsonSerializer.Serialize(
                    new Dictionary<string, string> { [key] = value });
                query = query.Where(q => EF.Functions.JsonContains(q.Tags, fragment));
            }
        }

        return query;
    }
}
