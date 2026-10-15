using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Evaluation.Domain;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Evaluation.Infrastructure.Persistence;

public sealed class ReportAttemptReader : IReportAttemptReader
{
    private readonly EvaluationDbContext _db;

    public ReportAttemptReader(EvaluationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ReportAttemptRow>> ListForAssignmentsAsync(
        IReadOnlyCollection<Guid> assignmentIds,
        CancellationToken cancellationToken)
    {
        if (assignmentIds.Count == 0)
        {
            return [];
        }

        var results = new List<ReportAttemptRow>();
        foreach (var chunk in assignmentIds.Distinct().Chunk(500))
        {
            var ids = chunk.ToArray();
            var rows = await _db.Attempts
                .AsNoTracking()
                .Where(attempt => ids.Contains(attempt.AssignmentId))
                .Select(attempt => new
                {
                    attempt.Id,
                    attempt.AssignmentId,
                    attempt.Status,
                    attempt.ResultStatus,
                    attempt.StartedAtUtc,
                    attempt.SubmittedAtUtc,
                    attempt.AutoPointsAwarded,
                    attempt.AutoPointsAvailable,
                    attempt.TotalPointsAvailable,
                    Items = attempt.ItemResults.Select(item => new
                    {
                        item.QuestionId,
                        item.SortOrder,
                        item.Type,
                        item.ScoringMode,
                        item.Points,
                        item.Status,
                        item.PointsAwarded
                    }).ToList()
                })
                .ToListAsync(cancellationToken);

            results.AddRange(rows.Select(row =>
            {
                var items = row.Items
                    .Select(item => new ReportAttemptItem(
                        item.QuestionId,
                        item.SortOrder,
                        item.Type,
                        item.ScoringMode,
                        item.Points,
                        item.Status == ItemScoreStatus.Scored ? "scored" : "unsettled",
                        item.PointsAwarded))
                    .ToArray();
                return new ReportAttemptRow(
                    row.Id,
                    row.AssignmentId,
                    row.Status == AttemptStatus.Submitted ? "submitted" : "inProgress",
                    row.ResultStatus switch
                    {
                        ResultStatus.Complete => "complete",
                        ResultStatus.Incomplete => "incomplete",
                        _ => null
                    },
                    row.StartedAtUtc,
                    row.SubmittedAtUtc,
                    row.AutoPointsAwarded,
                    row.AutoPointsAvailable,
                    row.TotalPointsAvailable,
                    items.Count(item => item.Status == "unsettled"),
                    items);
            }));
        }

        return results;
    }
}
