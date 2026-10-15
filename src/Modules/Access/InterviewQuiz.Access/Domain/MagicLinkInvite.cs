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

    /// <summary>Null means the invite has no time limit. Consume rejects the token once this is in the past.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Data-protected copy of the raw token so the same link can be emailed again.
    /// Consume still looks up the hash. Never log this value.
    /// </summary>
    public string? ProtectedToken { get; set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiresAt && expiresAt <= now;
}
