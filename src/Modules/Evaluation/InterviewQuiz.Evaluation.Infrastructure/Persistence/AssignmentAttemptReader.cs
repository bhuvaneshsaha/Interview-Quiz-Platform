using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Evaluation.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence;

public sealed class AssignmentAttemptReader : IAssignmentAttemptReader
{
    private readonly EvaluationDbContext _db;

    public AssignmentAttemptReader(EvaluationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AssignmentAttemptActivity>> ListForAssignmentsAsync(
        IReadOnlyCollection<Guid> assignmentIds,
        CancellationToken cancellationToken)
    {
        if (assignmentIds.Count == 0)
        {
            return [];
        }

        var rows = await _db.Attempts
            .AsNoTracking()
            .Where(attempt => assignmentIds.Contains(attempt.AssignmentId))
            .Select(attempt => new { attempt.AssignmentId, attempt.Status })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.AssignmentId)
            .Select(group => new AssignmentAttemptActivity(
                group.Key,
                Started: true,
                Submitted: group.Any(row => row.Status == AttemptStatus.Submitted)))
            .ToArray();
    }
}
