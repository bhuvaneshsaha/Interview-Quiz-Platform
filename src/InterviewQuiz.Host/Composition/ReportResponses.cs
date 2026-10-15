namespace InterviewQuiz.Host.Composition;

public class ReportWindow
{
    public bool DefaultWindowApplied { get; init; }
    public DateTimeOffset? AppliedCreatedFromUtc { get; init; }
    public DateTimeOffset? AppliedCreatedToUtc { get; init; }
}

public sealed class FunnelReportResponse : ReportWindow
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public FunnelReportRow Totals { get; init; } = new();
    public IReadOnlyList<FunnelReportRow> Items { get; init; } = [];
}

public sealed class FunnelReportRow
{
    public Guid? OpeningId { get; init; }
    public string OpeningTitle { get; init; } = "";
    public Guid? QuizId { get; init; }
    public string QuizTitle { get; init; } = "";
    public int Assigned { get; init; }
    public int Appeared { get; init; }
    public int Submitted { get; init; }
    public int Completed { get; init; }
    public int PendingReview { get; init; }
    public decimal AppearedRate { get; init; }
    public decimal SubmittedRate { get; init; }
    public decimal CompletedRate { get; init; }
}

public sealed class ScoresReportResponse : ReportWindow
{
    public decimal PassThreshold { get; init; } = ReportingMath.PassThreshold;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int SubmittedCount { get; init; }
    public int PassDenominator { get; init; }
    public int PassCount { get; init; }
    public decimal PassRate { get; init; }
    public decimal? AverageAutoPoints { get; init; }
    public decimal? MinAutoPoints { get; init; }
    public decimal? MaxAutoPoints { get; init; }
    public decimal? MedianAutoPoints { get; init; }
    public IReadOnlyList<ReportBucket> Histogram { get; init; } = [];
    public IReadOnlyList<ScoreReportRow> Items { get; init; } = [];
}

public sealed class ScoreReportRow
{
    public Guid AttemptId { get; init; }
    public Guid AssignmentId { get; init; }
    public Guid OpeningId { get; init; }
    public string OpeningTitle { get; init; } = "";
    public Guid QuizId { get; init; }
    public string QuizTitle { get; init; } = "";
    public string CandidateEmail { get; init; } = "";
    public string Mode { get; init; } = "";
    public string Status { get; init; } = "";
    public string? ResultStatus { get; init; }
    public decimal AutoPointsAwarded { get; init; }
    public decimal AutoPointsAvailable { get; init; }
    public decimal TotalPointsAvailable { get; init; }
    public int UnsettledCount { get; init; }
    public bool? Passed { get; init; }
    public Guid SnapshotId { get; init; }
    public long QuizRowVersion { get; init; }
}

public sealed class TimeReportResponse : ReportWindow
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int? P50Seconds { get; init; }
    public int? P90Seconds { get; init; }
    public IReadOnlyList<ReportBucket> Histogram { get; init; } = [];
    public IReadOnlyList<TimeReportRow> Items { get; init; } = [];
}

public sealed class TimeReportRow
{
    public Guid AttemptId { get; init; }
    public Guid AssignmentId { get; init; }
    public Guid OpeningId { get; init; }
    public string OpeningTitle { get; init; } = "";
    public Guid QuizId { get; init; }
    public string QuizTitle { get; init; } = "";
    public string CandidateEmail { get; init; } = "";
    public string Mode { get; init; } = "";
    public int DurationSeconds { get; init; }
    public bool PauseTracked { get; init; }
    public string? DurationNote { get; init; }
    public Guid SnapshotId { get; init; }
    public long QuizRowVersion { get; init; }
}

public sealed class QuestionsReportResponse : ReportWindow
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<QuestionReportRow> Items { get; init; } = [];
}

public sealed class QuestionReportRow
{
    public Guid QuestionId { get; init; }
    public int SortOrder { get; init; }
    public string Stem { get; init; } = "";
    public Guid OpeningId { get; init; }
    public string OpeningTitle { get; init; } = "";
    public Guid QuizId { get; init; }
    public string QuizTitle { get; init; } = "";
    public int SettledCount { get; init; }
    public decimal Difficulty { get; init; }
    public decimal? Discrimination { get; init; }
    public bool DiscriminationHidden { get; init; }
    public int SnapshotCount { get; init; }
}

public sealed class CandidatesReportResponse : ReportWindow
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<CandidateReportRow> Items { get; init; } = [];
}

public sealed class CandidateReportRow
{
    public int Rank { get; set; }
    public string CandidateEmail { get; init; } = "";
    public string Status { get; init; } = "";
    public decimal? AutoPointsAwarded { get; init; }
    public decimal? AutoPointsAvailable { get; init; }
    public int? DurationSeconds { get; init; }
    public string Mode { get; init; } = "";
    public Guid SnapshotId { get; init; }
    public long QuizRowVersion { get; init; }
    public Guid AssignmentId { get; init; }
    public Guid? AttemptId { get; init; }
    public Guid OpeningId { get; init; }
    public string OpeningTitle { get; init; } = "";
    public Guid QuizId { get; init; }
    public string QuizTitle { get; init; } = "";
}

public sealed class PendingReviewReportResponse : ReportWindow
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<PendingReviewReportRow> Items { get; init; } = [];
}

public sealed class PendingReviewReportRow
{
    public Guid AssignmentId { get; init; }
    public Guid? AttemptId { get; init; }
    public Guid OpeningId { get; init; }
    public string OpeningTitle { get; init; } = "";
    public Guid QuizId { get; init; }
    public string QuizTitle { get; init; } = "";
    public string CandidateEmail { get; init; } = "";
    public string Mode { get; init; } = "";
    public DateTimeOffset? SubmittedAtUtc { get; init; }
    public double AgeHours { get; init; }
    public int UnsettledCount { get; init; }
    public Guid SnapshotId { get; init; }
    public long QuizRowVersion { get; init; }
}

public sealed class ReportBucket
{
    public string Label { get; init; } = "";
    public int Count { get; init; }
}
