using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Kernel.Clock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Access.Authentication;

public sealed class MagicLinkInviteStore : IMagicLinkInviteStore
{
    private readonly AccessDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<MagicLinkInviteStore> _logger;

    public MagicLinkInviteStore(
        AccessDbContext db,
        IClock clock,
        ILogger<MagicLinkInviteStore> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<string> RotateAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var raw = RefreshTokenHasher.CreateToken();
        var now = _clock.UtcNow;

        var current = await _db.MagicLinkInvites
            .Where(invite => invite.AssignmentId == assignmentId && invite.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var invite in current)
        {
            invite.RevokedAt = now;
        }

        _db.MagicLinkInvites.Add(new MagicLinkInvite
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            TokenHash = RefreshTokenHasher.Hash(raw),
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Magic-link invite rotated for assignment {AssignmentId}", assignmentId);
        return raw;
    }

    public async Task<Guid?> FindActiveAssignmentIdAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var hash = RefreshTokenHasher.Hash(rawToken);
        var assignmentId = await _db.MagicLinkInvites
            .AsNoTracking()
            .Where(invite => invite.TokenHash == hash && invite.RevokedAt == null)
            .Select(invite => (Guid?)invite.AssignmentId)
            .SingleOrDefaultAsync(cancellationToken);

        return assignmentId;
    }
}
