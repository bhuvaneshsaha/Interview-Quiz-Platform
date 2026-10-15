using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/assignments")]
public sealed class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignments;
    private readonly IMagicLinkService _magicLinks;

    public AssignmentsController(IAssignmentService assignments, IMagicLinkService magicLinks)
    {
        _assignments = assignments;
        _magicLinks = magicLinks;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.Delivery.AssignmentsRead)]
    public async Task<ActionResult<PagedResult<AssignmentSummaryResponse>>> List(
        [FromQuery] AssignmentListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _assignments.ListAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.Delivery.AssignmentsRead)]
    public async Task<ActionResult<AssignmentResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(id, cancellationToken);
        return Ok(assignment);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Delivery.AssignmentsWrite)]
    public async Task<ActionResult<AssignmentResponse>> Create(
        [FromBody] CreateAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindUserId()
            ?? throw new ForbiddenException("Authenticated user id is required.");
        var created = await _assignments.CreateAsync(request, userId, cancellationToken);
        if (string.Equals(created.Mode, "async", StringComparison.OrdinalIgnoreCase))
        {
            var token = await _magicLinks.IssueAsync(created.Id, cancellationToken);
            created.InviteUrl = _magicLinks.BuildInviteUrl(token);
        }

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/invite")]
    [HasPermission(PermissionCodes.Delivery.AssignmentsWrite)]
    public async Task<ActionResult<InviteResponse>> Invite(Guid id, CancellationToken cancellationToken)
    {
        await _assignments.EnsureInvitableAsync(id, cancellationToken);
        var token = await _magicLinks.IssueAsync(id, cancellationToken);
        return Ok(new InviteResponse(id, _magicLinks.BuildInviteUrl(token)));
    }
}
