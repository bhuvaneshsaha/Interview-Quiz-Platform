using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Host.Composition;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/assignments/{assignmentId:guid}/live")]
public sealed class LiveSessionsController : ControllerBase
{
    private readonly LiveSessionCoordinator _live;

    public LiveSessionsController(LiveSessionCoordinator live)
    {
        _live = live;
    }

    [HttpPost("start")]
    [HasPermission(PermissionCodes.Delivery.SessionsLiveRun)]
    public Task<ActionResult<LiveProgressResponse>> Start(Guid assignmentId, CancellationToken cancellationToken)
        => Run(assignmentId, (id, actor, ct) => _live.StartAsync(id, actor, ct), cancellationToken);

    [HttpPost("pause")]
    [HasPermission(PermissionCodes.Delivery.SessionsLiveRun)]
    public Task<ActionResult<LiveProgressResponse>> Pause(Guid assignmentId, CancellationToken cancellationToken)
        => Run(assignmentId, (id, actor, ct) => _live.PauseAsync(id, actor, ct), cancellationToken);

    [HttpPost("resume")]
    [HasPermission(PermissionCodes.Delivery.SessionsLiveRun)]
    public Task<ActionResult<LiveProgressResponse>> Resume(Guid assignmentId, CancellationToken cancellationToken)
        => Run(assignmentId, (id, actor, ct) => _live.ResumeAsync(id, actor, ct), cancellationToken);

    [HttpPost("end")]
    [HasPermission(PermissionCodes.Delivery.SessionsLiveRun)]
    public Task<ActionResult<LiveProgressResponse>> End(Guid assignmentId, CancellationToken cancellationToken)
        => Run(assignmentId, (id, actor, ct) => _live.EndAsync(id, actor, ct), cancellationToken);

    [HttpGet("progress")]
    [HasAnyPermission(PermissionCodes.Delivery.SessionsLiveRun, PermissionCodes.Evaluation.AttemptsRead)]
    public async Task<ActionResult<LiveProgressResponse>> Progress(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var progress = await _live.ProgressAsync(assignmentId, cancellationToken);
        return Ok(progress);
    }

    private async Task<ActionResult<LiveProgressResponse>> Run(
        Guid assignmentId,
        Func<Guid, string, CancellationToken, Task<LiveProgressResponse>> action,
        CancellationToken cancellationToken)
    {
        var actor = User.FindUserId() ?? throw new ForbiddenException("Authenticated user id is required.");
        var progress = await action(assignmentId, actor, cancellationToken);
        return Ok(progress);
    }
}
