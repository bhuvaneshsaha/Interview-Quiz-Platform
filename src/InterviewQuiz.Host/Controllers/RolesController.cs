using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleAdminService _roles;

    public RolesController(IRoleAdminService roles)
    {
        _roles = roles;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Access.RolesManage)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> List(CancellationToken cancellationToken)
        => Ok(await _roles.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.Access.RolesManage)]
    public async Task<ActionResult<RoleResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await _roles.GetAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(PermissionCodes.Access.RolesManage)]
    public async Task<ActionResult<RoleResponse>> Create(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _roles.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Access.RolesManage)]
    public async Task<ActionResult<RoleResponse>> Update(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
        => Ok(await _roles.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Access.RolesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _roles.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/users")]
    [HasPermission(PermissionCodes.Access.RolesManage)]
    public async Task<IActionResult> AssignUser(
        Guid id,
        [FromBody] AssignRoleRequest request,
        CancellationToken cancellationToken)
    {
        await _roles.AssignUserAsync(id, request.UserId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/users/{userId}")]
    [HasPermission(PermissionCodes.Access.RolesManage)]
    public async Task<IActionResult> UnassignUser(
        Guid id,
        string userId,
        CancellationToken cancellationToken)
    {
        await _roles.UnassignUserAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
