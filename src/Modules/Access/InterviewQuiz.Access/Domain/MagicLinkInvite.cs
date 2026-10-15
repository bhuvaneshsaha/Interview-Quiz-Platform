namespace InterviewQuiz.Access.Domain;

/// <summary>
/// Hashed opaque candidate invite. Raw token is returned once per issue; consume does not burn it.
/// </summary>
public sealed class MagicLinkInvite
{
    public Guid Id { get; set; }

    public Guid AssignmentId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsRevoked => RevokedAt is not null;
}
