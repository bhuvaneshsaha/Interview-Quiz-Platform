using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Route("api/auth/magic-link")]
[AllowAnonymous]
public sealed class MagicLinkController : ControllerBase
{
    private readonly IMagicLinkService _magicLinks;

    public MagicLinkController(IMagicLinkService magicLinks)
    {
        _magicLinks = magicLinks;
    }

    [HttpPost("consume")]
    [AllowAnonymous]
    public async Task<ActionResult<CandidateTokenResponse>> Consume(
        [FromBody] ConsumeMagicLinkRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await _magicLinks.ConsumeAsync(request, cancellationToken);
        return Ok(tokens);
    }
}
