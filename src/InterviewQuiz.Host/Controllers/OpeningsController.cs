using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/openings")]
public sealed class OpeningsController : ControllerBase
{
    private readonly IOpeningService _openings;

    public OpeningsController(IOpeningService openings)
    {
        _openings = openings;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Openings.Read)]
    public async Task<ActionResult<PagedResult<OpeningResponse>>> List(
        [FromQuery] OpeningListQuery query,
        CancellationToken cancellationToken)
    {
        var criteria = OpeningListCriteria.FromQuery(query);
        var page = new PageRequest(query.Page, query.PageSize);
        var result = await _openings.ListAsync(criteria, page, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.Openings.Read)]
    public async Task<ActionResult<OpeningResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var opening = await _openings.GetAsync(id, cancellationToken);
        Response.Headers.ETag = $"\"{opening.RowVersion}\"";
        return Ok(opening);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Openings.Write)]
    public async Task<ActionResult<OpeningResponse>> Create(
        [FromBody] CreateOpeningRequest request,
        CancellationToken cancellationToken)
    {
        var opening = await _openings.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = opening.Id }, opening);
    }

    /// <summary>
    /// Architecture lists PUT /api/openings; the opening id is in the path (REST) and body.
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Openings.Write)]
    public async Task<ActionResult<OpeningResponse>> Update(
        Guid id,
        [FromBody] UpdateOpeningRequest request,
        CancellationToken cancellationToken)
    {
        request.Id = id;
        var opening = await _openings.UpdateAsync(request, cancellationToken);
        Response.Headers.ETag = $"\"{opening.RowVersion}\"";
        return Ok(opening);
    }

    [HttpPut]
    [HasPermission(PermissionCodes.Openings.Write)]
    public async Task<ActionResult<OpeningResponse>> UpdateFromCollection(
        [FromBody] UpdateOpeningRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "Id is required on PUT /api/openings.");
        }

        var opening = await _openings.UpdateAsync(request, cancellationToken);
        Response.Headers.ETag = $"\"{opening.RowVersion}\"";
        return Ok(opening);
    }
}
