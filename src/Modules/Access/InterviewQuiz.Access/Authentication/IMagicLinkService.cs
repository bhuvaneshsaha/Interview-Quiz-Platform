using InterviewQuiz.Access.Application.Contracts;

namespace InterviewQuiz.Access.Authentication;

public interface IMagicLinkService
{
    /// <summary>
    /// Issues a new opaque invite for the assignment and rotates any previous invite.
    /// Returns the raw token (not a URL). Delivery builds <c>{PublicBaseUrl}/attempt?token=</c>.
    /// </summary>
    Task<string> IssueAsync(Guid assignmentId, CancellationToken cancellationToken);

    Task<CandidateTokenResponse> ConsumeAsync(
        ConsumeMagicLinkRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds <c>{PublicBaseUrl}/attempt?token={opaque}</c>. Requires PublicBaseUrl with no trailing slash.
    /// </summary>
    string BuildInviteUrl(string opaqueToken);
}
