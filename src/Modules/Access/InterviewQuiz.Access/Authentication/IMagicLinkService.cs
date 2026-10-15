using InterviewQuiz.Access.Application.Contracts;

namespace InterviewQuiz.Access.Authentication;

public interface IMagicLinkService
{
    /// <summary>
    /// Issues a new opaque invite and revokes any previous one.
    /// <paramref name="expiresAt"/> null uses <c>Invite:DefaultLifetimeHours</c> (or no expiry when that is 0).
    /// Returns the raw token (not a URL).
    /// </summary>
    Task<string> IssueAsync(
        Guid assignmentId,
        CancellationToken cancellationToken,
        DateTimeOffset? expiresAt = null);

    Task<CandidateTokenResponse> ConsumeAsync(
        ConsumeMagicLinkRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds <c>{PublicBaseUrl}/attempt?token={opaque}</c>. Requires PublicBaseUrl with no trailing slash.
    /// </summary>
    string BuildInviteUrl(string opaqueToken);

    Task<MagicLinkInviteStatus> GetStatusAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task RevokeAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<MagicLinkInviteStatus> ExtendAsync(
        Guid assignmentId,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Raw token for the current unexpired invite, for email resend. Does not rotate.
    /// </summary>
    Task<string> GetDeliverableTokenAsync(Guid assignmentId, CancellationToken cancellationToken);
}
