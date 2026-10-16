using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Host.Composition;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/assignments")]
public sealed class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignments;
    private readonly ITemplateService _templates;
    private readonly AssignmentInviteCoordinator _invites;

    public AssignmentsController(
        IAssignmentService assignments,
        ITemplateService templates,
        AssignmentInviteCoordinator invites)
    {
        _assignments = assignments;
        _templates = templates;
        _invites = invites;
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
        await _invites.AttachStatusAsync(assignment, cancellationToken);
        return Ok(assignment);
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Delivery.AssignmentsWrite)]
    public async Task<ActionResult<AssignmentResponse>> Create(
        [FromBody] CreateAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var created = await _assignments.CreateAsync(request, userId, cancellationToken);
        await _invites.IssueAndEmailAsync(created, request.InviteExpiresAt, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("from-template")]
    [HasPermission(PermissionCodes.Delivery.AssignmentsWrite)]
    [HasPermission(PermissionCodes.Catalog.TemplatesRead)]
    public async Task<ActionResult<AssignmentResponse>> CreateFromTemplate(
        [FromBody] CreateAssignmentFromTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var versionId = request.TemplateVersionId;
        if (versionId is null || versionId == Guid.Empty)
        {
            var template = await _templates.GetAsync(request.TemplateId, cancellationToken);
            versionId = template.LatestVersionId;
        }

        var quiz = await _templates.CloneAsync(
            request.TemplateId,
            versionId.Value,
            new CloneTemplateVersionRequest
            {
                OpeningId = request.OpeningId,
                Title = request.QuizTitle
            },
            cancellationToken);

        var created = await _assignments.CreateAsync(
            new CreateAssignmentRequest
            {
                OpeningId = request.OpeningId,
                QuizId = quiz.Id,
                CandidateEmail = request.CandidateEmail,
                Mode = request.Mode,
                Timing = request.Timing,
                AttemptLimit = request.AttemptLimit,
                InviteExpiresAt = request.InviteExpiresAt,
                ShowScoreToCandidate = request.ShowScoreToCandidate
            },
            userId,
            cancellationToken);
        await _invites.IssueAndEmailAsync(created, request.InviteExpiresAt, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/invite")]
    [HasPermission(PermissionCodes.Delivery.AssignmentsWrite)]
    public async Task<ActionResult<InviteResponse>> Invite(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] IssueInviteRequest? request,
        CancellationToken cancellationToken)
    {
        var response = await _invites.ReissueAsync(id, request?.ExpiresAt, RequireUserId(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/invite/resend")]
    [HasPermission(PermissionCodes.Delivery.AssignmentsWrite)]
    public async Task<ActionResult<InviteDeliveryResponse>> Resend(Guid id, CancellationToken cancellationToken)
    {
        var response = await _invites.ResendAsync(id, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/invite/revoke")]
    [HasPermission(PermissionCodes.Delivery.AssignmentsWrite)]
    public async Task<ActionResult<InviteLifetimeResponse>> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var response = await _invites.RevokeAsync(id, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/invite/extend")]
    [HasPermission(PermissionCodes.Delivery.AssignmentsWrite)]
    public async Task<ActionResult<InviteLifetimeResponse>> Extend(
        Guid id,
        [FromBody] ExtendInviteRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _invites.ExtendAsync(id, request.ExpiresAt, cancellationToken);
        return Ok(response);
    }

    private string RequireUserId()
        => User.FindUserId() ?? throw new ForbiddenException("Authenticated user id is required.");
}
