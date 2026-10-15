using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Kernel.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserAdminService _users;
    private readonly IAssignableUserLookup _assignable;

    public UsersController(IUserAdminService users, IAssignableUserLookup assignable)
    {
        _users = users;
        _assignable = assignable;
    }

    [HttpGet("assignable")]
    [HasPermission(PermissionCodes.Openings.Read)]
    public async Task<ActionResult<IReadOnlyList<AssignableUserDto>>> SearchAssignable(
        [FromQuery] string? q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
        => Ok(await _assignable.SearchAsync(q, limit, cancellationToken));

    [HttpGet]
    [HasPermission(PermissionCodes.Access.UsersManage)]
    public async Task<ActionResult<PagedUsersResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await _users.ListAsync(page, pageSize, cancellationToken));

    [HttpGet("{id}")]
    [HasPermission(PermissionCodes.Access.UsersManage)]
    public async Task<ActionResult<UserResponse>> Get(string id, CancellationToken cancellationToken)
        => Ok(await _users.GetAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(PermissionCodes.Access.UsersManage)]
    public async Task<ActionResult<UserResponse>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [HasPermission(PermissionCodes.Access.UsersManage)]
    public async Task<ActionResult<UserResponse>> Update(
        string id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
        => Ok(await _users.UpdateAsync(id, request, cancellationToken));
}
