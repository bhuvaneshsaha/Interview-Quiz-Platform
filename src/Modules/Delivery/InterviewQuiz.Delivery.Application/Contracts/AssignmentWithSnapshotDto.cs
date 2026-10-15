using InterviewQuiz.Catalog.Application.Contracts;

namespace InterviewQuiz.Delivery.Application.Contracts;

/// <summary>
/// Delivery in-process header plus frozen <see cref="QuizSnapshotDto"/> (keys included).
/// </summary>
public sealed record AssignmentWithSnapshotDto(
    Guid Id,
    Guid OpeningId,
    Guid QuizId,
    Guid SnapshotId,
    string CandidateEmail,
    string Mode,
    int? OverallDurationMinutes,
    int AttemptLimit,
    string Status,
    QuizSnapshotDto Snapshot);
