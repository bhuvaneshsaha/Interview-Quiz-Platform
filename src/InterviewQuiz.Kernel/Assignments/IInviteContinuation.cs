namespace InterviewQuiz.Kernel.Assignments;

/// <summary>
/// Access-owned check for a candidate who already holds a JWT.
/// Revoke stops the attempt. Expiry does not.
/// </summary>
public interface IInviteContinuation
{
    Task<bool> IsRevokedAsync(Guid assignmentId, CancellationToken cancellationToken);
}

public static class InviteMessages
{
    public const string Expired = "This invite has expired.";

    public const string Revoked = "This invite has been revoked.";
}
