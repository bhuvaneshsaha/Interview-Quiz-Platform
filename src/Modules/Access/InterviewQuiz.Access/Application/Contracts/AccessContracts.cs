using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Access.Application.Contracts;

public sealed class LoginRequest
{
    public string Email { get; set; } = "";

    public string Password { get; set; } = "";
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = "";
}

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    string TokenType = "Bearer");

public sealed class ConsumeMagicLinkRequest
{
    public string Token { get; set; } = "";
}

public sealed record CandidateTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid AssignmentId,
    string TokenType = "Bearer");

public sealed record PermissionResponse(
    string Code,
    string DisplayName,
    string Module,
    bool IncludeInEmployeeRoleEditor);

public sealed record RoleResponse(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> PermissionCodes,
    IReadOnlyList<string> UserIds);

public sealed class CreateRoleRequest
{
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public IReadOnlyList<string> PermissionCodes { get; set; } = [];
}

public sealed class UpdateRoleRequest
{
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public IReadOnlyList<string> PermissionCodes { get; set; } = [];
}

public sealed class AssignRoleRequest
{
    public string UserId { get; set; } = "";
}

public sealed record MeResponse(
    string Id,
    string Email,
    IReadOnlyList<string> Permissions);

public sealed record UserResponse(
    string Id,
    string Email,
    bool IsDisabled,
    IReadOnlyList<string> RoleIds);

public sealed class CreateUserRequest
{
    public string Email { get; set; } = "";

    public string Password { get; set; } = "";
}

public sealed class UpdateUserRequest
{
    public bool IsDisabled { get; set; }
}

public sealed record PagedUsersResponse(
    IReadOnlyList<UserResponse> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public static PagedUsersResponse From(PagedResult<UserResponse> page)
        => new(page.Items, page.Page, page.PageSize, page.TotalCount);
}
