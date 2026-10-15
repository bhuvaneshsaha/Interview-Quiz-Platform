using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/ai-drafts")]
public sealed class AiDraftsController : ControllerBase
{
    private readonly IAiDraftService _drafts;

    public AiDraftsController(IAiDraftService drafts)
    {
        _drafts = drafts;
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Catalog.AiDraftUse)]
    public async Task<ActionResult<AiDraftResponse>> Create(
        [FromBody] CreateAiDraftRequest request,
        CancellationToken cancellationToken)
    {
        var draft = await _drafts.CreateAsync(request, RequireUserId(), cancellationToken);
        return Created($"/api/ai-drafts/{draft.Id}", draft);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.Catalog.AiDraftUse)]
    public async Task<ActionResult<AiDraftResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await _drafts.GetAsync(id, RequireUserId(), cancellationToken));

    [HttpPost("{id:guid}/accept")]
    [HasPermission(PermissionCodes.Catalog.AiDraftUse)]
    [HasPermission(PermissionCodes.Catalog.QuizzesWrite)]
    public async Task<ActionResult<AiDraftAcceptResponse>> Accept(
        Guid id,
        [FromBody] AcceptAiDraftRequest request,
        CancellationToken cancellationToken)
    {
        var accepted = await _drafts.AcceptAsync(id, request, RequireUserId(), cancellationToken);
        return Created($"/api/quizzes/{accepted.QuizId}", accepted);
    }

    private string RequireUserId()
        => User.FindUserId() ?? throw new ForbiddenException("Authenticated user id is required.");
}
