using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace InterviewQuiz.Access.Infrastructure.Seeding;

/// <summary>
/// Seeds the Super Admin system role and the Admin built-in role in every environment and, when configured, the first Super Admin holder.
/// New permission codes are re-synced onto Super Admin at startup. Admin keeps the permission edits an operator saved.
/// </summary>
public sealed class SuperAdminBootstrap
{
    public const string BootstrapActor = "bootstrap";
    public const string StartupCorrelation = "startup";

    private readonly AccessDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IBootstrapPasswordService _passwords;
    private readonly IOptions<BootstrapOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<SuperAdminBootstrap> _logger;

    public SuperAdminBootstrap(
        AccessDbContext db,
        UserManager<ApplicationUser> users,
        IBootstrapPasswordService passwords,
        IOptions<BootstrapOptions> options,
        IClock clock,
        ILogger<SuperAdminBootstrap> logger)
    {
        _db = db;
        _users = users;
        _passwords = passwords;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(cancellationToken);
        await EnsureAdminRoleAsync(cancellationToken);
        await EnsureUserAsync(cancellationToken);
    }

    private async Task EnsureRoleAsync(CancellationToken cancellationToken)
    {
        var catalog = await _db.Permissions
            .AsNoTracking()
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);
        var desired = SuperAdminPermissionSet.Codes()
            .Where(code => catalog.Contains(code, StringComparer.Ordinal))
            .ToArray();

        var role = await _db.Roles
            .Include(item => item.Permissions)
            .SingleOrDefaultAsync(item => item.Id == SuperAdminRole.Id, cancellationToken);

        if (role is null)
        {
            role = new AccessRole
            {
                Id = SuperAdminRole.Id,
                Name = SuperAdminRole.Name,
                Description = SuperAdminRole.Description,
                IsSystem = true,
                IsBuiltIn = false
            };
            _db.Roles.Add(role);
        }
        else
        {
            role.Name = SuperAdminRole.Name;
            role.Description = SuperAdminRole.Description;
            role.IsSystem = true;
            role.IsBuiltIn = false;
            role.Permissions.Clear();
        }

        foreach (var code in desired)
        {
            role.Permissions.Add(new RolePermission { RoleId = SuperAdminRole.Id, PermissionCode = code });
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Super Admin role {RoleId} synced with {PermissionCount} permissions",
            SuperAdminRole.Id,
            desired.Length);
    }

    private async Task EnsureAdminRoleAsync(CancellationToken cancellationToken)
    {
        await ReleaseAdminNameAsync(cancellationToken);

        var catalog = await _db.Permissions
            .AsNoTracking()
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);
        var employeeCodes = SuperAdminPermissionSet.Codes()
            .Where(code => catalog.Contains(code, StringComparer.Ordinal))
            .ToArray();

        var role = await _db.Roles
            .Include(item => item.Permissions)
            .Include(item => item.UserRoles)
            .SingleOrDefaultAsync(item => item.Id == AdminRole.Id, cancellationToken);

        if (role is null)
        {
            role = new AccessRole
            {
                Id = AdminRole.Id,
                Name = AdminRole.Name,
                Description = AdminRole.Description,
                IsSystem = false,
                IsBuiltIn = true,
                Permissions = employeeCodes
                    .Select(code => new RolePermission { RoleId = AdminRole.Id, PermissionCode = code })
                    .ToList()
            };
            _db.Roles.Add(role);
        }
        else
        {
            role.Name = AdminRole.Name;
            role.Description = AdminRole.Description;
            role.IsSystem = false;
            role.IsBuiltIn = true;
            var candidateRows = role.Permissions
                .Where(permission => string.Equals(
                    permission.PermissionCode,
                    PermissionCodes.Candidate.AttemptParticipate,
                    StringComparison.Ordinal))
                .ToList();
            foreach (var candidate in candidateRows)
            {
                role.Permissions.Remove(candidate);
            }
        }

        await FoldLegacyDevAdminAsync(role, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Admin role {RoleId} is built in with {PermissionCount} permissions",
            AdminRole.Id,
            role.Permissions.Count);
    }

    private async Task ReleaseAdminNameAsync(CancellationToken cancellationToken)
    {
        var clashes = await _db.Roles
            .Where(role => role.Id != AdminRole.Id && role.Name.ToLower() == AdminRole.Name.ToLower())
            .ToListAsync(cancellationToken);
        if (clashes.Count == 0)
        {
            return;
        }

        foreach (var clash in clashes)
        {
            var renamed = $"{AdminRole.Name} (custom)";
            var taken = await _db.Roles.AnyAsync(
                role => role.Id != clash.Id && role.Name == renamed,
                cancellationToken);
            clash.Name = taken ? $"{renamed} {clash.Id.ToString()[..8]}" : renamed;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task FoldLegacyDevAdminAsync(AccessRole admin, CancellationToken cancellationToken)
    {
        var legacyRoles = await _db.Roles
            .Include(role => role.UserRoles)
            .Where(role => role.Id != AdminRole.Id && role.Name == AdminRole.LegacyName)
            .ToListAsync(cancellationToken);
        if (legacyRoles.Count == 0)
        {
            return;
        }

        var holders = admin.UserRoles.Select(assignment => assignment.UserId).ToHashSet(StringComparer.Ordinal);
        foreach (var legacy in legacyRoles)
        {
            foreach (var assignment in legacy.UserRoles)
            {
                if (!holders.Add(assignment.UserId))
                {
                    continue;
                }

                admin.UserRoles.Add(new UserRoleAssignment
                {
                    UserId = assignment.UserId,
                    RoleId = admin.Id
                });
            }

            _db.Roles.Remove(legacy);
        }

        _logger.LogInformation(
            "Folded {LegacyRoleCount} Dev Admin roles into {RoleId}",
            legacyRoles.Count,
            AdminRole.Id);
    }

    private async Task EnsureUserAsync(CancellationToken cancellationToken)
    {
        var email = _options.Value.SuperAdminEmail?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            return;
        }

        var user = await _users.FindByEmailAsync(email);
        var created = false;
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsDisabled = false,
                MustSetPassword = true,
                CreatedAt = _clock.UtcNow
            };
            var result = await _users.CreateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to bootstrap Super Admin: "
                    + string.Join(" ", result.Errors.Select(error => error.Description)));
            }

            created = true;
        }

        var assigned = await _db.UserRoles.AnyAsync(
            assignment => assignment.UserId == user.Id && assignment.RoleId == SuperAdminRole.Id,
            cancellationToken);
        if (!assigned)
        {
            _db.UserRoles.Add(new UserRoleAssignment { UserId = user.Id, RoleId = SuperAdminRole.Id });
            _db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                ActorUserId = BootstrapActor,
                Action = SuperAdminRole.AssignedAction,
                TargetUserId = user.Id,
                OccurredAtUtc = _clock.UtcNow,
                CorrelationId = StartupCorrelation
            });
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Assigned Super Admin to {UserId}. CorrelationId {CorrelationId}",
                user.Id,
                StartupCorrelation);
        }

        if (!user.MustSetPassword && !created)
        {
            return;
        }

        var expired = user.BootstrapTokenExpiresAt is null || user.BootstrapTokenExpiresAt <= _clock.UtcNow;
        var missing = string.IsNullOrWhiteSpace(user.BootstrapTokenHash);
        if (!_options.Value.ResetSuperAdminSetup && !missing && !expired && !created)
        {
            return;
        }

        var token = _passwords.IssueToken(user);
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to store Super Admin setup token: "
                + string.Join(" ", updated.Errors.Select(error => error.Description)));
        }

        _logger.LogWarning(
            "Super Admin {Email} must set a password before sign-in. One-time setup token (treat as a secret, single use, expires in {Hours} hours): {Token}",
            email,
            BootstrapPasswordService.TokenLifetime.TotalHours,
            token);
    }

    public static bool IsMissingSchema(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                return true;
            }
        }

        return false;
    }
}
