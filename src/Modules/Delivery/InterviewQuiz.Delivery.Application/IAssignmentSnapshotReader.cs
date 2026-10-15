using InterviewQuiz.Delivery.Application.Contracts;

namespace InterviewQuiz.Delivery.Application;

/// <summary>
/// Frozen assignment + snapshot for Evaluation. Keys stay on this in-process DTO.
/// </summary>
public interface IAssignmentSnapshotReader
{
    Task<AssignmentWithSnapshotDto?> GetAssignmentWithSnapshotAsync(
        Guid assignmentId,
        CancellationToken cancellationToken);
}
