using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Delivery.Infrastructure.Persistence;

public sealed class AssignmentRepository : IAssignmentRepository
{
    private readonly DeliveryDbContext _db;

    public AssignmentRepository(DeliveryDbContext db)
    {
        _db = db;
    }

    public Task<Assignment?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.Assignments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Assignment assignment, CancellationToken cancellationToken)
        => await _db.Assignments.AddAsync(assignment, cancellationToken);

    public async Task<PagedResult<Assignment>> ListAsync(
        Guid? openingId,
        string? keyword,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = _db.Assignments.AsNoTracking().AsQueryable();

        if (openingId is { } opening && opening != Guid.Empty)
        {
            query = query.Where(a => a.OpeningId == opening);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLower();
            query = query.Where(a =>
                a.CandidateEmail.ToLower().Contains(term)
                || a.Snapshot.Title.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Assignment>(items, page.Page, page.PageSize, total);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Assignment was modified by another request. Reload and retry.");
        }
    }
}
