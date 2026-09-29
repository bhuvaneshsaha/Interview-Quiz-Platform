using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Identity;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Access.Application.Services;

public sealed class RoleAdminService : IRoleAdminService
{
    private readonly AccessDbContext _db;
    private readonly IIdentityUserDirectory _users;

    public RoleAdminService(AccessDbContext db, IIdentityUserDirectory users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var roles = await QueryRoles().ToListAsync(cancellationToken);
        return roles.Select(Map).ToArray();
    }

    public async Task<RoleResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await QueryRoles().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(AccessRole), id);
        return Map(role);
    }

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        await EnsureNameAvailableAsync(name, exceptRoleId: null, cancellationToken);
        var permissionCodes = await ValidatePermissionCodesAsync(request.PermissionCodes, cancellationToken);

        var role = new AccessRole
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = NormalizeDescription(request.Description),
            Permissions = permissionCodes
                .Select(code => new RolePermission { PermissionCode = code })
                .ToList()
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(role.Id, cancellationToken);
    }

    public async Task<RoleResponse> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var role = await _db.Roles
            .Include(item => item.Permissions)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(AccessRole), id);

        var name = NormalizeName(request.Name);
        await EnsureNameAvailableAsync(name, exceptRoleId: id, cancellationToken);
        var permissionCodes = await ValidatePermissionCodesAsync(request.PermissionCodes, cancellationToken);

        role.Name = name;
        role.Description = NormalizeDescription(request.Description);
        role.Permissions.Clear();
        foreach (var code in permissionCodes)
        {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(role.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(AccessRole), id);
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignUserAsync(Guid roleId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        _ = await GetAsync(roleId, cancellationToken);
        _ = await _users.FindByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);

        var exists = await _db.UserRoles.AnyAsync(
            assignment => assignment.RoleId == roleId && assignment.UserId == userId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        _db.UserRoles.Add(new UserRoleAssignment { RoleId = roleId, UserId = userId });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnassignUserAsync(Guid roleId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var assignment = await _db.UserRoles.SingleOrDefaultAsync(
            item => item.RoleId == roleId && item.UserId == userId,
            cancellationToken);
        if (assignment is null)
        {
            return;
        }

        _db.UserRoles.Remove(assignment);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<AccessRole> QueryRoles()
        => _db.Roles
            .AsNoTracking()
            .Include(role => role.Permissions)
            .Include(role => role.UserRoles)
            .OrderBy(role => role.Name);

    private static RoleResponse Map(AccessRole role)
        => new(
            role.Id,
            role.Name,
            role.Description,
            role.Permissions
                .Select(permission => permission.PermissionCode)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray(),
            role.UserRoles
                .Select(assignment => assignment.UserId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());

    private async Task EnsureNameAvailableAsync(
        string name,
        Guid? exceptRoleId,
        CancellationToken cancellationToken)
    {
        var clash = await _db.Roles.AnyAsync(
            role => role.Name == name && (!exceptRoleId.HasValue || role.Id != exceptRoleId.Value),
            cancellationToken);
        if (clash)
        {
            throw new DomainException($"A role named '{name}' already exists.");
        }
    }

    private async Task<IReadOnlyList<string>> ValidatePermissionCodesAsync(
        IReadOnlyList<string>? codes,
        CancellationToken cancellationToken)
    {
        var requested = (codes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (requested.Length == 0)
        {
            return requested;
        }

        var catalog = await _db.Permissions
            .AsNoTracking()
            .Where(permission => requested.Contains(permission.Code))
            .ToListAsync(cancellationToken);

        var unknown = requested.Except(catalog.Select(p => p.Code), StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            throw new DomainException($"Unknown permission code(s): {string.Join(", ", unknown)}.");
        }

        var notEditable = catalog.Where(p => !p.IncludeInEmployeeRoleEditor).Select(p => p.Code).ToArray();
        if (notEditable.Length > 0)
        {
            throw new DomainException(
                $"Permission code(s) cannot be assigned on employee roles: {string.Join(", ", notEditable)}.");
        }

        return requested;
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new DomainException("Role name is required.");
        }

        if (trimmed.Length > 128)
        {
            throw new DomainException("Role name must be 128 characters or fewer.");
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
