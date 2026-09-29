using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Openings.Infrastructure.Persistence;

public sealed class OpeningFieldDefinitionRepository : IOpeningFieldDefinitionRepository
{
    private readonly OpeningsDbContext _db;

    public OpeningFieldDefinitionRepository(OpeningsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OpeningFieldDefinition>> ListAsync(CancellationToken cancellationToken)
        => await _db.OpeningFieldDefinitions
            .AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Key)
            .ToListAsync(cancellationToken);

    public async Task ReplaceAllAsync(
        IReadOnlyList<OpeningFieldDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            await _db.OpeningFieldDefinitions.ExecuteDeleteAsync(cancellationToken);
            await _db.OpeningFieldDefinitions.AddRangeAsync(definitions, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
