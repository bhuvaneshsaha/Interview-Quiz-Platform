namespace InterviewQuiz.Delivery.Application.Contracts;

public class AssignmentSummaryResponse
{
    public Guid Id { get; init; }
    public Guid OpeningId { get; init; }
    public Guid QuizId { get; init; }
    public Guid SnapshotId { get; init; }
    public string SnapshotTitle { get; init; } = "";
    public int SnapshotQuestionCount { get; init; }

    /// <summary>Quiz rowVersion copied into the snapshot. Results are for this revision only.</summary>
    public long SnapshotQuizRowVersion { get; init; }
    public string CandidateEmail { get; init; } = "";
    public string Mode { get; init; } = "";
    public int? OverallDurationMinutes { get; init; }
    public int AttemptLimit { get; init; }
    public bool ShowScoreToCandidate { get; init; }
    public string Status { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
