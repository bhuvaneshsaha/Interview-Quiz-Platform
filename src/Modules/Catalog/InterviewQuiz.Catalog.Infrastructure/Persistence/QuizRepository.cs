using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence;

public sealed class QuizRepository : IQuizRepository
{
    private readonly CatalogDbContext _db;

    public QuizRepository(CatalogDbContext db)
    {
        _db = db;
    }

    public Task<Quiz?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.Quizzes.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task AddAsync(Quiz quiz, CancellationToken cancellationToken)
        => await _db.Quizzes.AddAsync(quiz, cancellationToken);

    public void SetExpectedRowVersion(Quiz quiz, uint expectedRowVersion)
        => _db.Entry(quiz).Property(q => q.RowVersion).OriginalValue = expectedRowVersion;

    public async Task<PagedResult<Quiz>> ListAsync(
        QuizListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(_db.Quizzes.AsNoTracking(), criteria);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(q => q.UpdatedAtUtc)
            .ThenBy(q => q.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Quiz>(items, page.Page, page.PageSize, total);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Quiz was modified by another request. Reload and retry.");
        }
    }

    private static IQueryable<Quiz> ApplyFilters(IQueryable<Quiz> query, QuizListCriteria criteria)
    {
        if (criteria.OpeningId is { } openingId && openingId != Guid.Empty)
        {
            query = query.Where(q => q.OpeningId == openingId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword.Trim().ToLower();
            query = query.Where(q => q.Title.ToLower().Contains(keyword));
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
