namespace InterviewQuiz.Access.Authentication;

public sealed record IssuedRefreshToken(string RawToken, DateTimeOffset ExpiresAt, Guid Id);

public sealed record RefreshTokenRotation(string UserId, IssuedRefreshToken Replacement);

public interface IRefreshTokenStore
{
    Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken cancellationToken);

    Task<RefreshTokenRotation?> RotateAsync(string rawToken, CancellationToken cancellationToken);

    Task RevokeAsync(string rawToken, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken);
}
