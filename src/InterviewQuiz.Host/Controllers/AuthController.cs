using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewQuiz.Host.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IBootstrapPasswordService _bootstrapPasswords;

    public AuthController(IAuthService auth, IBootstrapPasswordService bootstrapPasswords)
    {
        _auth = auth;
        _bootstrapPasswords = bootstrapPasswords;
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await _auth.LoginAsync(request, cancellationToken);
        if (tokens is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Invalid email or password.");
        }

        return Ok(tokens);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await _auth.RefreshAsync(request, cancellationToken);
        if (tokens is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Refresh token is invalid or expired.");
        }

        return Ok(tokens);
    }

    [HttpPost("bootstrap/set-password")]
    public async Task<IActionResult> SetBootstrapPassword(
        [FromBody] SetBootstrapPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _bootstrapPasswords.CompleteAsync(
            request.Email,
            request.Token,
            request.NewPassword,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(request, cancellationToken);
        return NoContent();
    }
}
