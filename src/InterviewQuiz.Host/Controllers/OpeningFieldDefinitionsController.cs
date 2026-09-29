using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/opening-field-definitions")]
public sealed class OpeningFieldDefinitionsController : ControllerBase
{
    private readonly IOpeningFieldDefinitionService _definitions;

    public OpeningFieldDefinitionsController(IOpeningFieldDefinitionService definitions)
    {
        _definitions = definitions;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Openings.FieldsManage)]
    public async Task<ActionResult<IReadOnlyList<OpeningFieldDefinitionResponse>>> Get(CancellationToken cancellationToken)
        => Ok(await _definitions.ListAsync(cancellationToken));

    [HttpPut]
    [HasPermission(PermissionCodes.Openings.FieldsManage)]
    public async Task<ActionResult<IReadOnlyList<OpeningFieldDefinitionResponse>>> Replace(
        [FromBody] ReplaceOpeningFieldDefinitionsRequest request,
        CancellationToken cancellationToken)
        => Ok(await _definitions.ReplaceAsync(request, cancellationToken));
}
