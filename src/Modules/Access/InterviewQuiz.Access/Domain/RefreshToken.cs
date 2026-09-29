namespace InterviewQuiz.Access.Domain;

/// <summary>
/// Server-side refresh token. Only the SHA-256 hash is stored; the raw value is returned once.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public required string UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
