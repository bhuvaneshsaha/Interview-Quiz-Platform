using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Search.Application;
using InterviewQuiz.Search.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Search.Infrastructure.Persistence;

public sealed class FilterRepository : IFilterRepository
{
    private readonly SearchDbContext _db;

    public FilterRepository(SearchDbContext db)
    {
        _db = db;
    }

    public Task<SavedFilter?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.Filters.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task AddAsync(SavedFilter filter, CancellationToken cancellationToken)
        => await _db.Filters.AddAsync(filter, cancellationToken);

    public void Remove(SavedFilter filter)
        => _db.Filters.Remove(filter);

    public async Task<PagedResult<SavedFilter>> ListVisibleAsync(
        string userId,
        FilterTarget? target,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = _db.Filters.AsNoTracking().Where(filter =>
            filter.OwnerUserId == userId
            || filter.ShareMode == FilterShareMode.PublicInsideCompany
            || (filter.ShareMode == FilterShareMode.SpecificUsers
                && filter.SharedWithUserIds.Contains(userId)));

        if (target is { } filterTarget)
        {
            query = query.Where(filter => filter.Target == filterTarget);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(filter => filter.UpdatedAtUtc)
            .ThenBy(filter => filter.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SavedFilter>(items, page.Page, page.PageSize, total);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _db.SaveChangesAsync(cancellationToken);
}
