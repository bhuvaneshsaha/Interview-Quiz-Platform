using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Delivery.Infrastructure.Persistence;

public sealed class ReportAssignmentReader : IReportAssignmentReader
{
    private readonly DeliveryDbContext _db;

    public ReportAssignmentReader(DeliveryDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ReportAssignmentRow>> ListAsync(
        ReportAssignmentQuery query,
        CancellationToken cancellationToken)
    {
        var rows = _db.Assignments.AsNoTracking();

        if (query.OpeningIds is { Count: > 0 })
        {
            var ids = query.OpeningIds.ToArray();
            rows = rows.Where(assignment => ids.Contains(assignment.OpeningId));
        }

        if (query.QuizIds is { Count: > 0 })
        {
            var ids = query.QuizIds.ToArray();
            rows = rows.Where(assignment => ids.Contains(assignment.QuizId));
        }

        if (query.CreatedFromUtc is { } from)
        {
            rows = query.CreatedFromExclusive
                ? rows.Where(assignment => assignment.CreatedAtUtc > from)
                : rows.Where(assignment => assignment.CreatedAtUtc >= from);
        }

        if (query.CreatedToUtc is { } to)
        {
            rows = query.CreatedToExclusive
                ? rows.Where(assignment => assignment.CreatedAtUtc < to)
                : rows.Where(assignment => assignment.CreatedAtUtc <= to);
        }

        if (query.Statuses is { Count: > 0 })
        {
            var statuses = query.Statuses.Select(ParseStatus).ToArray();
            rows = rows.Where(assignment => statuses.Contains(assignment.Status));
        }

        if (!string.IsNullOrWhiteSpace(query.Mode))
        {
            var mode = ParseMode(query.Mode);
            rows = rows.Where(assignment => assignment.Mode == mode);
        }

        if (query.SnapshotId is { } snapshotId)
        {
            rows = rows.Where(assignment => assignment.SnapshotId == snapshotId);
        }

        if (query.QuizRowVersions is { Count: > 0 })
        {
            var versions = query.QuizRowVersions.ToArray();
            rows = rows.Where(assignment => versions.Contains(assignment.Snapshot.QuizRowVersion));
        }

        var projected = await rows
            .Select(assignment => new
            {
                assignment.Id,
                assignment.OpeningId,
                assignment.QuizId,
                assignment.CandidateEmail,
                assignment.Mode,
                assignment.Status,
                assignment.CreatedAtUtc,
                assignment.SnapshotId,
                assignment.TotalPausedSeconds,
                Title = assignment.Snapshot.Title,
                assignment.Snapshot.QuizRowVersion,
                assignment.Snapshot.Payload
            })
            .ToListAsync(cancellationToken);

        return projected
            .Select(row => new ReportAssignmentRow(
                row.Id,
                row.OpeningId,
                row.QuizId,
                row.CandidateEmail,
                row.Mode == AssignmentMode.Live ? "live" : "async",
                Assignment.ToWire(row.Status),
                row.CreatedAtUtc,
                row.SnapshotId,
                row.Title,
                row.QuizRowVersion,
                row.TotalPausedSeconds,
                Questions(row.Payload)))
            .ToArray();
    }

    private static IReadOnlyList<ReportAssignmentQuestion> Questions(System.Text.Json.JsonDocument payload)
    {
        try
        {
            var snapshot = SnapshotJson.ToDto(payload);
            return snapshot.Questions
                .OrderBy(question => question.SortOrder)
                .ThenBy(question => question.Id)
                .Select(question => new ReportAssignmentQuestion(
                    question.Id,
                    question.SortOrder,
                    question.Stem,
                    ScoringWire(question.ScoringMode),
                    question.Points))
                .ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private static string ScoringWire(ScoringMode mode)
        => mode switch
        {
            ScoringMode.Auto => "auto",
            ScoringMode.AiAssist => "aiAssist",
            ScoringMode.HumanOnly => "humanOnly",
            _ => "auto"
        };

    private static AssignmentStatus ParseStatus(string wire)
        => wire switch
        {
            "notStarted" => AssignmentStatus.NotStarted,
            "inProgress" => AssignmentStatus.InProgress,
            "pendingReview" => AssignmentStatus.PendingReview,
            "completed" => AssignmentStatus.Completed,
            _ => throw new DomainException("Filter value for status is invalid.")
        };

    private static AssignmentMode ParseMode(string wire)
        => wire switch
        {
            "async" => AssignmentMode.Async,
            "live" => AssignmentMode.Live,
            _ => throw new DomainException("Filter value for mode is invalid.")
        };
}
