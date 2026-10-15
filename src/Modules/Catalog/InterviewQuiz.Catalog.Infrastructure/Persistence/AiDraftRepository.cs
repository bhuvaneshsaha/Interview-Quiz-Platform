using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Catalog.Infrastructure.Persistence;

public sealed class AiDraftRepository : IAiDraftRepository
{
    private readonly CatalogDbContext _db;

    public AiDraftRepository(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AiDraft draft, CancellationToken cancellationToken)
        => await _db.AiDrafts.AddAsync(draft, cancellationToken);

    public Task<AiDraft?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.AiDrafts.FirstOrDefaultAsync(draft => draft.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _db.SaveChangesAsync(cancellationToken);
}
