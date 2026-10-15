namespace InterviewQuiz.Evaluation.Application.Contracts;

/// <summary>
/// Read-only welcome facts. Does not create an attempt and does not include questions or <c>dueAtUtc</c>.
/// </summary>
public sealed class CandidateAttemptPreviewResponse
{
    /// <summary><c>notStarted</c>, <c>inProgress</c>, or <c>submitted</c>.</summary>
    public string AttemptStatus { get; init; } = "notStarted";

    public string QuizTitle { get; init; } = "";
    public string OpeningName { get; init; } = "";
    public int DurationMinutes { get; init; }
    public int QuestionCount { get; init; }

    /// <summary>Live only: <c>waiting</c>, <c>running</c>, <c>paused</c>, or <c>ended</c>.</summary>
    public string? LiveState { get; init; }
}
