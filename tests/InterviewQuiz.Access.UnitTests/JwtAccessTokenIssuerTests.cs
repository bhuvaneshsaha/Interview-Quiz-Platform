using System.Security.Claims;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace InterviewQuiz.Access.UnitTests;

public sealed class JwtAccessTokenIssuerTests
{
    [Fact]
    public async Task Valid_token_contains_permission_claims()
    {
        var jwt = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        var issuer = new JwtAccessTokenIssuer(Options.Create(jwt), new FixedClock(now));
        var issued = issuer.Issue("user-1", "recruiter.dev@example.com", [
            PermissionCodes.Openings.Read,
            PermissionCodes.Openings.Write
        ]);

        var result = await ValidateAsync(issued.Token, jwt);

        Assert.True(result.IsValid, result.Exception?.ToString());
        var permissions = result.ClaimsIdentity!.FindAll(PermissionClaims.Permission)
            .Select(c => c.Value)
            .ToArray();
        Assert.Contains(PermissionCodes.Openings.Write, permissions);
        Assert.NotNull(
            result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)
            ?? result.ClaimsIdentity.FindFirst(ClaimTypes.NameIdentifier));
        Assert.Equal(now.AddMinutes(15), issued.ExpiresAt);
    }

    [Fact]
    public async Task Candidate_token_has_assignment_scope_and_only_participate_permission()
    {
        var jwt = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        var issuer = new JwtAccessTokenIssuer(Options.Create(jwt), new FixedClock(now));
        var assignmentId = Guid.Parse("7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001");
        var issued = issuer.IssueCandidate("user-9", "candidate.dev@example.com", assignmentId, attemptId: null);

        var result = await ValidateAsync(issued.Token, jwt);

        Assert.True(result.IsValid, result.Exception?.ToString());
        var permissions = result.ClaimsIdentity!.FindAll(PermissionClaims.Permission)
            .Select(c => c.Value)
            .ToArray();
        Assert.Equal([PermissionCodes.Candidate.AttemptParticipate], permissions);
        Assert.Equal(
            assignmentId.ToString("D"),
            result.ClaimsIdentity.FindFirst(PermissionClaims.AssignmentId)?.Value);
        Assert.Null(result.ClaimsIdentity.FindFirst(PermissionClaims.AttemptId));
        Assert.Equal(now.AddMinutes(60), issued.ExpiresAt);
    }

    [Fact]
    public async Task Candidate_token_includes_attempt_id_when_provided()
    {
        var jwt = CreateOptions();
        var issuer = new JwtAccessTokenIssuer(Options.Create(jwt), new FixedClock(DateTimeOffset.UtcNow));
        var assignmentId = Guid.Parse("7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001");
        var attemptId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var issued = issuer.IssueCandidate("user-9", "candidate.dev@example.com", assignmentId, attemptId);

        var result = await ValidateAsync(issued.Token, jwt);

        Assert.True(result.IsValid, result.Exception?.ToString());
        Assert.Equal(
            attemptId.ToString("D"),
            result.ClaimsIdentity!.FindFirst(PermissionClaims.AttemptId)?.Value);
    }

    [Fact]
    public async Task Expired_token_fails_validation()
    {
        var jwt = CreateOptions();
        var issuer = new JwtAccessTokenIssuer(
            Options.Create(jwt),
            new FixedClock(DateTimeOffset.UtcNow.AddMinutes(-20)));
        var issued = issuer.Issue("user-1", "recruiter.dev@example.com", [PermissionCodes.Openings.Read]);

        var result = await ValidateAsync(issued.Token, jwt);

        Assert.False(result.IsValid);
        Assert.True(
            result.Exception is SecurityTokenExpiredException or SecurityTokenInvalidLifetimeException,
            result.Exception?.GetType().FullName);
    }

    [Fact]
    public async Task Wrong_audience_fails_validation()
    {
        var jwt = CreateOptions();
        var issuer = new JwtAccessTokenIssuer(Options.Create(jwt), new FixedClock(DateTimeOffset.UtcNow));
        var issued = issuer.Issue("user-1", "recruiter.dev@example.com", [PermissionCodes.Openings.Read]);

        var wrongAudience = CreateOptions();
        wrongAudience.Audience = "someone-else";
        var result = await ValidateAsync(issued.Token, wrongAudience);

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenInvalidAudienceException>(result.Exception);
    }

    [Fact]
    public async Task Missing_token_fails_validation()
    {
        var result = await ValidateAsync("", CreateOptions());

        Assert.False(result.IsValid);
    }

    private static JwtOptions CreateOptions()
        => new()
        {
            Issuer = "InterviewQuiz.Tests",
            Audience = "InterviewQuiz.Tests",
            SigningKey = "InterviewQuiz-Testing-Signing-Key-Not-For-Production!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
            CandidateAccessTokenMinutes = 60
        };

    private static async Task<TokenValidationResult> ValidateAsync(string token, JwtOptions jwt)
    {
        var handler = new JsonWebTokenHandler();
        return await handler.ValidateTokenAsync(token, JwtTokenValidation.Create(jwt));
    }
}
