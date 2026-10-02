namespace InterviewQuiz.Access.Authentication;

public interface IMagicLinkInviteStore
{
    /// <summary>
    /// Hashes and stores a new invite for the assignment, revoking any previous current invite.
    /// Returns the raw opaque token once.
    /// </summary>
    Task<string> RotateAsync(Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the assignment id for an active (not rotated) invite, or null if unknown/revoked.
    /// </summary>
    Task<Guid?> FindActiveAssignmentIdAsync(string rawToken, CancellationToken cancellationToken);
}
