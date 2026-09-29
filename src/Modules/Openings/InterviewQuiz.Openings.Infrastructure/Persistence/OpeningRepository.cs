using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Openings.Infrastructure.Persistence;

public sealed class OpeningRepository : IOpeningRepository
{
    private readonly OpeningsDbContext _db;

    public OpeningRepository(OpeningsDbContext db)
    {
        _db = db;
    }

    public Task<Opening?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.Openings.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task AddAsync(Opening opening, CancellationToken cancellationToken)
        => await _db.Openings.AddAsync(opening, cancellationToken);

    public void SetExpectedRowVersion(Opening opening, uint expectedRowVersion)
        => _db.Entry(opening).Property(o => o.RowVersion).OriginalValue = expectedRowVersion;

    public async Task<PagedResult<Opening>> ListAsync(
        OpeningListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(_db.Openings.AsNoTracking(), criteria);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.StartDate)
            .ThenBy(o => o.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Opening>(items, page.Page, page.PageSize, total);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Opening was modified by another request. Reload and retry.");
        }
    }

    private static IQueryable<Opening> ApplyFilters(IQueryable<Opening> query, OpeningListCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.Owner))
        {
            var owner = criteria.Owner.Trim();
            query = query.Where(o => o.Owner == owner);
        }

        if (criteria.ExperienceMinYears is { } minYears)
        {
            query = query.Where(o => o.ExpectedExperienceYears >= minYears);
        }

        if (criteria.ExperienceMaxYears is { } maxYears)
        {
            query = query.Where(o => o.ExpectedExperienceYears <= maxYears);
        }

        if (criteria.StartDateFrom is { } startFrom)
        {
            query = query.Where(o => o.StartDate >= startFrom);
        }

        if (criteria.StartDateTo is { } startTo)
        {
            query = query.Where(o => o.StartDate <= startTo);
        }

        if (criteria.ExpectedCloseDateFrom is { } closeFrom)
        {
            query = query.Where(o => o.ExpectedCloseDate != null && o.ExpectedCloseDate >= closeFrom);
        }

        if (criteria.ExpectedCloseDateTo is { } closeTo)
        {
            query = query.Where(o => o.ExpectedCloseDate != null && o.ExpectedCloseDate <= closeTo);
        }

        if (criteria.Tags is { Count: > 0 })
        {
            foreach (var (key, value) in criteria.Tags)
            {
                var fragment = System.Text.Json.JsonSerializer.Serialize(
                    new Dictionary<string, string> { [key] = value });
                query = query.Where(o => EF.Functions.JsonContains(o.Tags, fragment));
            }
        }

        return query;
    }
}
