namespace InterviewQuiz.Delivery.Application;

public sealed record ReportAssignmentQuestion(
    Guid QuestionId,
    int SortOrder,
    string Stem,
    string ScoringMode,
    int Points);

/// <summary>
/// Assignment facts for Reporting v1. Snapshot question bodies (answer keys) are not included.
/// </summary>
public sealed record ReportAssignmentRow(
    Guid AssignmentId,
    Guid OpeningId,
    Guid QuizId,
    string CandidateEmail,
    string Mode,
    string Status,
    DateTimeOffset CreatedAtUtc,
    Guid SnapshotId,
    string SnapshotTitle,
    long QuizRowVersion,
    int TotalPausedSeconds,
    IReadOnlyList<ReportAssignmentQuestion> Questions);

public sealed class ReportAssignmentQuery
{
    public IReadOnlyCollection<Guid>? OpeningIds { get; init; }
    public IReadOnlyCollection<Guid>? QuizIds { get; init; }
    public DateTimeOffset? CreatedFromUtc { get; init; }
    public bool CreatedFromExclusive { get; init; }
    public DateTimeOffset? CreatedToUtc { get; init; }
    public bool CreatedToExclusive { get; init; }
    public IReadOnlyCollection<string>? Statuses { get; init; }
    public string? Mode { get; init; }
    public Guid? SnapshotId { get; init; }
    public IReadOnlyCollection<long>? QuizRowVersions { get; init; }
}

/// <summary>
/// Company-wide assignment rows for host reporting. Callers do not read the delivery schema.
/// </summary>
public interface IReportAssignmentReader
{
    Task<IReadOnlyList<ReportAssignmentRow>> ListAsync(
        ReportAssignmentQuery query,
        CancellationToken cancellationToken);
}
