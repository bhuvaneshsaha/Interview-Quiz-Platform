namespace InterviewQuiz.Delivery.Application.Contracts;

public sealed record LiveSessionView(
    Guid AssignmentId,
    string Mode,
    string AssignmentStatus,
    int? OverallDurationMinutes,
    string State,
    DateTimeOffset? LiveStartedAtUtc,
    DateTimeOffset? PausedAtUtc,
    int TotalPausedSeconds,
    DateTimeOffset? LiveEndedAtUtc,
    string? EndReason,
    DateTimeOffset? CandidateLastSeenAtUtc,
    int? CurrentQuestionIndex,
    DateTimeOffset? LastActivityAtUtc,
    int QuestionCount);

public sealed class LiveProgressResponse
{
    public Guid AssignmentId { get; init; }
    public Guid? AttemptId { get; init; }
    public string State { get; init; } = "";
    public bool CandidateConnected { get; init; }
    public DateTimeOffset? LastActivityAtUtc { get; init; }
    public int? CurrentQuestionIndex { get; init; }
    public int QuestionCount { get; init; }
    public int AnsweredCount { get; init; }
    public int FlaggedCount { get; init; }
    public int? RemainingSeconds { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? PausedAtUtc { get; init; }
    public int TotalPausedSeconds { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
    public string? EndReason { get; init; }
}

public sealed class ReportPositionRequest
{
    public int QuestionIndex { get; set; }
}
