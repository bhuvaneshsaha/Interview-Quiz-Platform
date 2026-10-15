namespace InterviewQuiz.Evaluation.Application;

public sealed record ReportAttemptItem(
    Guid QuestionId,
    int SortOrder,
    string Type,
    string ScoringMode,
    int Points,
    string Status,
    decimal? PointsAwarded);

/// <summary>
/// Attempt facts for Reporting v1. Candidate answers are not included.
/// </summary>
public sealed record ReportAttemptRow(
    Guid AttemptId,
    Guid AssignmentId,
    string Status,
    string? ResultStatus,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    decimal AutoPointsAwarded,
    decimal AutoPointsAvailable,
    decimal TotalPointsAvailable,
    int UnsettledCount,
    IReadOnlyList<ReportAttemptItem> Items);

/// <summary>
/// Attempt rows for assignment ids. Callers do not read the evaluation schema.
/// </summary>
public interface IReportAttemptReader
{
    Task<IReadOnlyList<ReportAttemptRow>> ListForAssignmentsAsync(
        IReadOnlyCollection<Guid> assignmentIds,
        CancellationToken cancellationToken);
}
