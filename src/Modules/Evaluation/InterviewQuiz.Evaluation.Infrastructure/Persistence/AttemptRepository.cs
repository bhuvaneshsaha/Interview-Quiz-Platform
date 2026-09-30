using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence;

public sealed class AttemptRepository : IAttemptRepository
{
    private readonly EvaluationDbContext _db;

    public AttemptRepository(EvaluationDbContext db)
    {
        _db = db;
    }

    public Task<Attempt?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.Attempts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Attempt?> GetInProgressAsync(Guid assignmentId, CancellationToken cancellationToken)
        => _db.Attempts.FirstOrDefaultAsync(
            a => a.AssignmentId == assignmentId && a.Status == AttemptStatus.InProgress,
            cancellationToken);

    public Task<Attempt?> GetLatestAsync(Guid assignmentId, CancellationToken cancellationToken)
        => _db.Attempts
            .Where(a => a.AssignmentId == assignmentId)
            .OrderByDescending(a => a.StartedAtUtc)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountSubmittedAsync(Guid assignmentId, CancellationToken cancellationToken)
        => _db.Attempts.CountAsync(
            a => a.AssignmentId == assignmentId && a.Status == AttemptStatus.Submitted,
            cancellationToken);

    public async Task AddAsync(Attempt attempt, CancellationToken cancellationToken)
        => await _db.Attempts.AddAsync(attempt, cancellationToken);

    public async Task<PagedResult<Attempt>> ListByAssignmentAsync(
        Guid assignmentId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = _db.Attempts.AsNoTracking().Where(a => a.AssignmentId == assignmentId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.StartedAtUtc)
            .ThenBy(a => a.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Attempt>(items, page.Page, page.PageSize, total);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException(Attempt.AlreadySubmittedMessage);
        }
    }
}
