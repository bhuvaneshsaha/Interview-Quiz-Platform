using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Access.Authentication;

public sealed class MagicLinkInviteStore : IMagicLinkInviteStore
{
    public const string NoInviteToRevokeMessage = "There is no invite to revoke.";
    public const string NoInviteToExtendMessage = "There is no invite to extend.";
    public const string ExpiryMustBeFutureMessage = "Invite expiry must be in the future.";

    private readonly AccessDbContext _db;
    private readonly IClock _clock;
    private readonly IDataProtector _protector;
    private readonly ILogger<MagicLinkInviteStore> _logger;

    public MagicLinkInviteStore(
        AccessDbContext db,
        IClock clock,
        IDataProtectionProvider dataProtection,
        ILogger<MagicLinkInviteStore> logger)
    {
        _db = db;
        _clock = clock;
        _protector = dataProtection.CreateProtector("InterviewQuiz.MagicLinkInvite.v1");
        _logger = logger;
    }

    public async Task<string> RotateAsync(
        Guid assignmentId,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        var raw = RefreshTokenHasher.CreateToken();
        var now = _clock.UtcNow;

        var current = await _db.MagicLinkInvites
            .Where(invite => invite.AssignmentId == assignmentId && invite.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var invite in current)
        {
            invite.RevokedAt = now;
            invite.ProtectedToken = null;
        }

        _db.MagicLinkInvites.Add(new MagicLinkInvite
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            TokenHash = RefreshTokenHasher.Hash(raw),
            CreatedAt = now,
            ExpiresAt = expiresAt,
            ProtectedToken = _protector.Protect(raw)
        });
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Magic-link invite rotated for assignment {AssignmentId} expires {ExpiresAt}",
            assignmentId,
            expiresAt);
        return raw;
    }

    public async Task<MagicLinkLookup> FindAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return MagicLinkLookup.Unknown;
        }

        var hash = RefreshTokenHasher.Hash(rawToken);
        var invite = await _db.MagicLinkInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.TokenHash == hash, cancellationToken);
        if (invite is null)
        {
            return MagicLinkLookup.Unknown;
        }

        if (invite.RevokedAt is not null)
        {
            return new MagicLinkLookup(MagicLinkLookupKind.Revoked, invite.AssignmentId);
        }

        if (invite.IsExpired(_clock.UtcNow))
        {
            return new MagicLinkLookup(MagicLinkLookupKind.Expired, invite.AssignmentId);
        }

        return new MagicLinkLookup(MagicLinkLookupKind.Active, invite.AssignmentId);
    }

    public async Task<MagicLinkInviteStatus> GetStatusAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var current = await _db.MagicLinkInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                invite => invite.AssignmentId == assignmentId && invite.RevokedAt == null,
                cancellationToken);
        if (current is not null)
        {
            var expired = current.IsExpired(_clock.UtcNow);
            return new MagicLinkInviteStatus(
                expired ? MagicLinkInviteStatus.Expired : MagicLinkInviteStatus.Active,
                current.ExpiresAt);
        }

        var any = await _db.MagicLinkInvites
            .AsNoTracking()
            .AnyAsync(invite => invite.AssignmentId == assignmentId, cancellationToken);
        return any
            ? new MagicLinkInviteStatus(MagicLinkInviteStatus.Revoked, null)
            : MagicLinkInviteStatus.Missing;
    }

    public async Task RevokeCurrentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var current = await CurrentAsync(assignmentId, cancellationToken)
            ?? throw new DomainException(NoInviteToRevokeMessage);
        current.RevokedAt = _clock.UtcNow;
        current.ProtectedToken = null;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Magic-link invite revoked for assignment {AssignmentId}", assignmentId);
    }

    public async Task<MagicLinkInviteStatus> ExtendCurrentAsync(
        Guid assignmentId,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        if (expiresAt is { } value && value <= _clock.UtcNow)
        {
            throw new DomainException(ExpiryMustBeFutureMessage);
        }

        var current = await CurrentAsync(assignmentId, cancellationToken)
            ?? throw new DomainException(NoInviteToExtendMessage);
        current.ExpiresAt = expiresAt;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Magic-link invite expiry updated for assignment {AssignmentId} expires {ExpiresAt}",
            assignmentId,
            expiresAt);

        var expired = current.IsExpired(_clock.UtcNow);
        return new MagicLinkInviteStatus(
            expired ? MagicLinkInviteStatus.Expired : MagicLinkInviteStatus.Active,
            current.ExpiresAt);
    }

    public async Task<string?> ReadCurrentTokenAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var current = await _db.MagicLinkInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                invite => invite.AssignmentId == assignmentId && invite.RevokedAt == null,
                cancellationToken);
        if (current?.ProtectedToken is not { Length: > 0 } protectedToken)
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedToken);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            _logger.LogWarning(
                "Magic-link protected token could not be read for assignment {AssignmentId}",
                assignmentId);
            return null;
        }
    }

    private Task<MagicLinkInvite?> CurrentAsync(Guid assignmentId, CancellationToken cancellationToken)
        => _db.MagicLinkInvites.SingleOrDefaultAsync(
            invite => invite.AssignmentId == assignmentId && invite.RevokedAt == null,
            cancellationToken);
}
