using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class AttemptsController : ControllerBase
{
    private readonly IAttemptService _attempts;

    public AttemptsController(IAttemptService attempts)
    {
        _attempts = attempts;
    }

    [HttpGet("assignments/{assignmentId:guid}/attempts")]
    [HasPermission(PermissionCodes.Evaluation.AttemptsRead)]
    public async Task<ActionResult<PagedResult<AttemptSummaryResponse>>> ListByAssignment(
        Guid assignmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _attempts.ListByAssignmentAsync(
            assignmentId,
            new PageRequest(page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("attempts/{id:guid}")]
    [HasPermission(PermissionCodes.Evaluation.AttemptsRead)]
    public async Task<ActionResult<AttemptResultResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _attempts.GetResultAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("attempts/{id:guid}/review")]
    [HasPermission(PermissionCodes.Evaluation.AttemptsReview)]
    public async Task<ActionResult<AttemptReviewResponse>> GetReview(Guid id, CancellationToken cancellationToken)
    {
        var result = await _attempts.GetReviewAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("attempts/{id:guid}/items/{itemId:guid}/review")]
    [HasPermission(PermissionCodes.Evaluation.AttemptsReview)]
    public async Task<ActionResult<AttemptReviewResponse>> ReviewItem(
        Guid id,
        Guid itemId,
        [FromBody] ReviewItemRequest request,
        CancellationToken cancellationToken)
    {
        var reviewerUserId = User.FindUserId()
            ?? throw new ForbiddenException("Authenticated user id is required.");
        var result = await _attempts.ScoreItemAsync(id, itemId, request, reviewerUserId, cancellationToken);
        return Ok(result);
    }
}
