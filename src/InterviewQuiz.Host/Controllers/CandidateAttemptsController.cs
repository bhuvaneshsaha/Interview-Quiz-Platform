using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Host.Composition;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Authorize]
[Route("api/assignments/{assignmentId:guid}/attempts")]
public sealed class CandidateAttemptsController : ControllerBase
{
    private readonly IAttemptService _attempts;
    private readonly IAttemptClock _clock;
    private readonly LiveSessionCoordinator _live;

    public CandidateAttemptsController(
        IAttemptService attempts,
        IAttemptClock clock,
        LiveSessionCoordinator live)
    {
        _attempts = attempts;
        _clock = clock;
        _live = live;
    }

    [HttpPost]
    [HasPermission(PermissionCodes.Candidate.AttemptParticipate)]
    public async Task<ActionResult<CandidateAttemptResponse>> Start(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        User.EnsureAssignmentScope(assignmentId);
        var (response, created) = await _attempts.StartAsync(assignmentId, cancellationToken);
        if (created)
        {
            return CreatedAtAction(nameof(GetCurrent), new { assignmentId }, response);
        }

        return Ok(response);
    }

    [HttpGet("preview")]
    [HasPermission(PermissionCodes.Candidate.AttemptParticipate)]
    public async Task<ActionResult<CandidateAttemptPreviewResponse>> Preview(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        User.EnsureAssignmentScope(assignmentId);
        var response = await _attempts.GetPreviewAsync(assignmentId, cancellationToken);
        return Ok(response);
    }

    [HttpGet("current")]
    [HasPermission(PermissionCodes.Candidate.AttemptParticipate)]
    public async Task<ActionResult<CandidateAttemptResponse>> GetCurrent(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        User.EnsureAssignmentScope(assignmentId);
        var response = await _attempts.GetCurrentAsync(assignmentId, cancellationToken);
        return Ok(response);
    }

    [HttpPut("current/answers")]
    [HasPermission(PermissionCodes.Candidate.AttemptParticipate)]
    public async Task<ActionResult<CandidateAttemptResponse>> SaveAnswers(
        Guid assignmentId,
        [FromBody] SaveAnswersRequest request,
        CancellationToken cancellationToken)
    {
        User.EnsureAssignmentScope(assignmentId);
        var response = await _attempts.SaveAnswersAsync(assignmentId, request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("current/submit")]
    [HasPermission(PermissionCodes.Candidate.AttemptParticipate)]
    public async Task<ActionResult<CandidateSubmitResponse>> Submit(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        User.EnsureAssignmentScope(assignmentId);
        var response = await _attempts.SubmitAsync(assignmentId, cancellationToken);
        return Ok(response);
    }

    [HttpPut("current/position")]
    [HasPermission(PermissionCodes.Candidate.AttemptParticipate)]
    public async Task<IActionResult> ReportPosition(
        Guid assignmentId,
        [FromBody] ReportPositionRequest request,
        CancellationToken cancellationToken)
    {
        User.EnsureAssignmentScope(assignmentId);
        await _live.SetPositionAsync(assignmentId, request.QuestionIndex, cancellationToken);
        return NoContent();
    }

    [HttpPut("current/flags")]
    [HasPermission(PermissionCodes.Candidate.AttemptParticipate)]
    public async Task<ActionResult<CandidateAttemptResponse>> SetFlags(
        Guid assignmentId,
        [FromBody] SetReviewFlagsRequest request,
        CancellationToken cancellationToken)
    {
        User.EnsureAssignmentScope(assignmentId);
        var flags = (request.Flags ?? [])
            .Select(flag => new ReviewFlagUpdate(flag.QuestionId, flag.Flagged))
            .ToList();
        await _clock.SetFlagsAsync(assignmentId, flags, cancellationToken);
        var current = await _attempts.GetCurrentAsync(assignmentId, cancellationToken);
        return Ok(current);
    }
}
