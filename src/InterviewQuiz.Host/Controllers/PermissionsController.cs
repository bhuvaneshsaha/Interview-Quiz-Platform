using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/permissions")]
public sealed class PermissionsController : ControllerBase
{
    private readonly IPermissionCatalogService _catalog;

    public PermissionsController(IPermissionCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Access.RolesManage)]
    public async Task<ActionResult<IReadOnlyList<PermissionResponse>>> List(CancellationToken cancellationToken)
        => Ok(await _catalog.ListAsync(cancellationToken));
}
