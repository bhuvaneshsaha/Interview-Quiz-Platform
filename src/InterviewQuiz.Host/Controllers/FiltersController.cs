using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Search.Application;
using InterviewQuiz.Search.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/filters")]
public sealed class FiltersController : ControllerBase
{
    private readonly IFilterService _filters;

    public FiltersController(IFilterService filters)
    {
        _filters = filters;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<FilterResponse>>> List(
        [FromQuery] FilterListQuery query,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var page = new PageRequest(query.Page, query.PageSize);
        var result = await _filters.ListAsync(userId, query.Target, page, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FilterResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var filter = await _filters.GetAsync(userId, id, cancellationToken);
        return Ok(filter);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Search.FiltersWrite)]
    public async Task<ActionResult<FilterResponse>> Create(
        [FromBody] CreateFilterRequest request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var created = await _filters.CreateAsync(userId, request, cancellationToken);
        return Created($"/api/filters/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.Search.FiltersWrite)]
    public async Task<ActionResult<FilterResponse>> Update(
        Guid id,
        [FromBody] UpdateFilterRequest request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var updated = await _filters.UpdateAsync(userId, id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.Search.FiltersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await _filters.DeleteAsync(userId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/share")]
    [HasPermission(PermissionCodes.Search.FiltersShare)]
    public async Task<ActionResult<FilterResponse>> Share(
        Guid id,
        [FromBody] ShareFilterRequest request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var shared = await _filters.ShareAsync(userId, id, request, cancellationToken);
        return Ok(shared);
    }

    [HttpPost("{id:guid}/unshare")]
    [HasPermission(PermissionCodes.Search.FiltersShare)]
    public async Task<ActionResult<FilterResponse>> Unshare(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var unshared = await _filters.UnshareAsync(userId, id, cancellationToken);
        return Ok(unshared);
    }

    private string? CurrentUserId() => User.FindUserId();
}
