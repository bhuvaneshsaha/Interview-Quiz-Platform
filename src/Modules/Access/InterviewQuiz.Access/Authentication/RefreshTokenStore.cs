using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Kernel.Clock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Access.Authentication;

public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly AccessDbContext _db;
    private readonly IClock _clock;
    private readonly JwtOptions _jwt;
    private readonly ILogger<RefreshTokenStore> _logger;

    public RefreshTokenStore(
        AccessDbContext db,
        IClock clock,
        IOptions<JwtOptions> jwt,
        ILogger<RefreshTokenStore> logger)
    {
        _db = db;
        _clock = clock;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public async Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var raw = RefreshTokenHasher.CreateToken();
        var now = _clock.UtcNow;
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = RefreshTokenHasher.Hash(raw),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays)
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(raw, entity.ExpiresAt, entity.Id);
    }

    public async Task<RefreshTokenRotation?> RotateAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var hash = RefreshTokenHasher.Hash(rawToken);
        var now = _clock.UtcNow;
        var existing = await _db.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (existing.IsRevoked)
        {
            await RevokeAllForUserAsync(existing.UserId, cancellationToken);
            _logger.LogWarning("Revoked refresh tokens for user {UserId} after reuse of a revoked token", existing.UserId);
            return null;
        }

        if (existing.IsExpired(now))
        {
            existing.RevokedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        var replacement = await IssueAsync(existing.UserId, cancellationToken);
        existing.RevokedAt = now;
        existing.ReplacedByTokenId = replacement.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return new RefreshTokenRotation(existing.UserId, replacement);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var hash = RefreshTokenHasher.Hash(rawToken);
        var existing = await _db.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null || existing.IsRevoked)
        {
            return;
        }

        existing.RevokedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var now = _clock.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }
}
