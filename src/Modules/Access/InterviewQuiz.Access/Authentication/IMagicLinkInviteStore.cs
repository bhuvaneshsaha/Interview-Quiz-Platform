namespace InterviewQuiz.Access.Authentication;

public interface IMagicLinkInviteStore
{
    /// <summary>
    /// Hashes and stores a new invite, revoking any previous current invite.
    /// Returns the raw opaque token once.
    /// </summary>
    Task<string> RotateAsync(Guid assignmentId, DateTimeOffset? expiresAt, CancellationToken cancellationToken);

    /// <summary>
    /// Classifies a raw token as active, expired, revoked, or unknown. Does not log the token.
    /// </summary>
    Task<MagicLinkLookup> FindAsync(string rawToken, CancellationToken cancellationToken);

    Task<MagicLinkInviteStatus> GetStatusAsync(Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the current invite without issuing a replacement.
    /// </summary>
    Task RevokeCurrentAsync(Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates expiry on the current invite without rotating the token.
    /// Null clears the time limit.
    /// </summary>
    Task<MagicLinkInviteStatus> ExtendCurrentAsync(
        Guid assignmentId,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the raw token for the current invite so it can be emailed again.
    /// Null when there is no current invite or the protected value cannot be read.
    /// </summary>
    Task<string?> ReadCurrentTokenAsync(Guid assignmentId, CancellationToken cancellationToken);
}
