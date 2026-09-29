using InterviewQuiz.Access.Application.Contracts;

namespace InterviewQuiz.Access.Application;

public interface IEffectivePermissionReader
{
    Task<IReadOnlyList<string>> GetAsync(string userId, CancellationToken cancellationToken);
}

public interface IPermissionCatalogService
{
    Task<IReadOnlyList<PermissionResponse>> ListAsync(CancellationToken cancellationToken);
}

public interface ICurrentUserQuery
{
    MeResponse GetMe(System.Security.Claims.ClaimsPrincipal principal);

    IReadOnlyList<string> GetPermissions(System.Security.Claims.ClaimsPrincipal principal);
}

public interface IRoleAdminService
{
    Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken cancellationToken);

    Task<RoleResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    Task<RoleResponse> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task AssignUserAsync(Guid roleId, string userId, CancellationToken cancellationToken);

    Task UnassignUserAsync(Guid roleId, string userId, CancellationToken cancellationToken);
}

public interface IUserAdminService
{
    Task<PagedUsersResponse> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<UserResponse> GetAsync(string userId, CancellationToken cancellationToken);

    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserResponse> UpdateAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken);
}
