using InterviewQuiz.Catalog.Application;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence;

public sealed class OpeningQuizReader : IOpeningQuizReader
{
    private readonly CatalogDbContext _db;

    public OpeningQuizReader(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OpeningQuizItem>> ListByOpeningAsync(
        Guid openingId,
        CancellationToken cancellationToken)
    {
        return await _db.Quizzes
            .AsNoTracking()
            .Where(quiz => quiz.OpeningId == openingId)
            .OrderBy(quiz => quiz.Title)
            .ThenBy(quiz => quiz.Id)
            .Select(quiz => new OpeningQuizItem(
                quiz.Id,
                quiz.Title,
                quiz.Questions.Count,
                quiz.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
