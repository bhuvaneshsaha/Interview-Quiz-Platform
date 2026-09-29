using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace InterviewQuiz.Access.UnitTests;

public sealed class JwtAccessTokenIssuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Valid_token_contains_permission_claims()
    {
        var jwt = CreateOptions();
        var issuer = new JwtAccessTokenIssuer(Options.Create(jwt), new FixedClock(Now));
        var issued = issuer.Issue("user-1", "recruiter.dev@example.com", [
            PermissionCodes.Openings.Read,
            PermissionCodes.Openings.Write
        ]);

        var result = await ValidateAsync(issued.Token, jwt);

        Assert.True(result.IsValid);
        Assert.Contains(
            result.ClaimsIdentity!.FindAll(PermissionClaims.Permission).Select(c => c.Value),
            code => code == PermissionCodes.Openings.Write);
        Assert.Equal("user-1", result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(Now.AddMinutes(15), issued.ExpiresAt);
    }

    [Fact]
    public async Task Expired_token_fails_validation()
    {
        var jwt = CreateOptions();
        var issuer = new JwtAccessTokenIssuer(
            Options.Create(jwt),
            new FixedClock(Now.AddMinutes(-20)));
        var issued = issuer.Issue("user-1", "recruiter.dev@example.com", [PermissionCodes.Openings.Read]);

        var result = await ValidateAsync(issued.Token, jwt);

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(result.Exception);
    }

    [Fact]
    public async Task Wrong_audience_fails_validation()
    {
        var jwt = CreateOptions();
        var issuer = new JwtAccessTokenIssuer(Options.Create(jwt), new FixedClock(Now));
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
            RefreshTokenDays = 7
        };

    private static async Task<TokenValidationResult> ValidateAsync(string token, JwtOptions jwt)
    {
        var handler = new JsonWebTokenHandler();
        return await handler.ValidateTokenAsync(token, JwtTokenValidation.Create(jwt));
    }
}
