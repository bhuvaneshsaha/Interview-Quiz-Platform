using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Evaluation.Application.Contracts;
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

    public CandidateAttemptsController(IAttemptService attempts)
    {
        _attempts = attempts;
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
}
