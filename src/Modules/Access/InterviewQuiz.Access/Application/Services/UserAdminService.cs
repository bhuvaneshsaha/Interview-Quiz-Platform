using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Identity;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Access.Application.Services;

public sealed class UserAdminService : IUserAdminService
{
    private readonly IIdentityUserDirectory _users;
    private readonly AccessDbContext _db;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IClock _clock;

    public UserAdminService(
        IIdentityUserDirectory users,
        AccessDbContext db,
        IRefreshTokenStore refreshTokens,
        IClock clock)
    {
        _users = users;
        _db = db;
        _refreshTokens = refreshTokens;
        _clock = clock;
    }

    public async Task<PagedUsersResponse> ListAsync(
        int page,
        int pageSize,
        bool includeCandidates,
        CancellationToken cancellationToken)
    {
        var request = new PageRequest(page, pageSize);
        var total = await _users.CountAsync(includeCandidates, cancellationToken);
        var users = await _users.ListAsync(request.Skip, request.PageSize, includeCandidates, cancellationToken);
        var ids = users.Select(user => user.Id).ToArray();
        var roleMap = await LoadRolesAsync(ids, cancellationToken);

        var items = users
            .Select(user => Map(user, roleMap.GetValueOrDefault(user.Id, [])))
            .ToArray();

        return new PagedUsersResponse(items, request.Page, request.PageSize, total);
    }

    public async Task<UserResponse> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        var roles = await LoadRolesAsync([user.Id], cancellationToken);
        return Map(user, roles.GetValueOrDefault(user.Id, []));
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new DomainException("Password is required.");
        }

        var existing = await _users.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            throw new DomainException("A user with that email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsDisabled = false,
            CreatedAt = _clock.UtcNow
        };

        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new DomainException(string.Join(" ", created.Errors));
        }

        return new UserResponse(user.Id, user.Email!, user.IsDisabled, [], false, []);
    }

    public async Task<UserResponse> UpdateAsync(
        string userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        if (request.IsDisabled && !user.IsDisabled)
        {
            await EnsureNotLastActiveSuperAdminAsync(user.Id, cancellationToken);
        }

        user.IsDisabled = request.IsDisabled;
        await _users.UpdateAsync(user, cancellationToken);

        if (user.IsDisabled)
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        var roles = await LoadRolesAsync([user.Id], cancellationToken);
        return Map(user, roles.GetValueOrDefault(user.Id, []));
    }

    private async Task EnsureNotLastActiveSuperAdminAsync(string userId, CancellationToken cancellationToken)
    {
        var holds = await _db.UserRoles.AsNoTracking().AnyAsync(
            assignment => assignment.RoleId == SuperAdminRole.Id && assignment.UserId == userId,
            cancellationToken);
        if (!holds)
        {
            return;
        }

        var active = await (
            from assignment in _db.UserRoles.AsNoTracking()
            join user in _db.Users.AsNoTracking() on assignment.UserId equals user.Id
            where assignment.RoleId == SuperAdminRole.Id && !user.IsDisabled
            select user.Id).CountAsync(cancellationToken);
        if (active <= 1)
        {
            throw new ConflictException(SuperAdminRole.LastHolderDeactivateMessage);
        }
    }

    private async Task<ApplicationUser> RequireUserAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return await _users.FindByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);
    }

    private async Task<Dictionary<string, IReadOnlyList<UserRoleLabel>>> LoadRolesAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<UserRoleLabel>>();
        }

        var rows = await (
            from assignment in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where userIds.Contains(assignment.UserId)
            select new { assignment.UserId, RoleId = assignment.RoleId.ToString(), role.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UserRoleLabel>)group
                    .Select(row => new UserRoleLabel(row.RoleId, row.Name))
                    .OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(role => role.Id, StringComparer.Ordinal)
                    .ToArray());
    }

    private static UserResponse Map(ApplicationUser user, IReadOnlyList<UserRoleLabel> roles)
        => new(
            user.Id,
            user.Email ?? user.UserName ?? "",
            user.IsDisabled,
            roles.Select(role => role.Id).ToArray(),
            string.IsNullOrEmpty(user.PasswordHash),
            roles.Select(role => role.Name).ToArray());

    private sealed record UserRoleLabel(string Id, string Name);

    private static string NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new DomainException("Email is required.");
        }

        return trimmed;
    }
}
