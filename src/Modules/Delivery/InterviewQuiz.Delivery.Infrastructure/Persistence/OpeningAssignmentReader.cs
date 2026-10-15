using InterviewQuiz.Delivery.Application;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Delivery.Infrastructure.Persistence;

public sealed class OpeningAssignmentReader : IOpeningAssignmentReader
{
    private readonly DeliveryDbContext _db;

    public OpeningAssignmentReader(DeliveryDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OpeningAssignmentItem>> ListByOpeningAsync(
        Guid openingId,
        CancellationToken cancellationToken)
    {
        return await _db.Assignments
            .AsNoTracking()
            .Where(assignment => assignment.OpeningId == openingId)
            .Select(assignment => new OpeningAssignmentItem(
                assignment.Id,
                assignment.QuizId,
                assignment.CandidateEmail))
            .ToListAsync(cancellationToken);
    }
}
