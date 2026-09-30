namespace InterviewQuiz.Kernel.Assignments;

/// <summary>
/// Delivery-owned lookup for Access magic-link issue/consume. Access must not query delivery tables.
/// </summary>
public interface IAssignmentInviteInfo
{
    Task<AssignmentInviteInfoDto?> GetAsync(Guid assignmentId, CancellationToken cancellationToken);
}

public sealed record AssignmentInviteInfoDto(
    Guid AssignmentId,
    string CandidateEmail,
    string Mode,
    string Status);
