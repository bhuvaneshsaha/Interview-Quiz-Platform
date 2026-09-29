using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserAdminService _users;

    public UsersController(IUserAdminService users)
    {
        _users = users;
    }

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
