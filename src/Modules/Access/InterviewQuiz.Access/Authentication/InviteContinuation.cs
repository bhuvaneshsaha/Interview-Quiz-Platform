using InterviewQuiz.Kernel.Assignments;

namespace InterviewQuiz.Access.Authentication;

public sealed class InviteContinuation : IInviteContinuation
{
    private readonly IMagicLinkInviteStore _invites;

    public InviteContinuation(IMagicLinkInviteStore invites)
    {
        _invites = invites;
    }

    public async Task<bool> IsRevokedAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var status = await _invites.GetStatusAsync(assignmentId, cancellationToken);
        return status.State == MagicLinkInviteStatus.Revoked;
    }
}
