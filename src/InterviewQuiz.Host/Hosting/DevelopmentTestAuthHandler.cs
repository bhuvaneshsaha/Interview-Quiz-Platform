using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Host.Hosting;

/// <summary>
/// Development/Testing-only authentication. Send <c>Authorization: Test {user}</c>.
/// Auth will replace this with JWT bearer. Do not use in Production.
/// </summary>
public sealed class DevelopmentTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevelopmentTest";
    public const string HeaderPrefix = "Test ";

    public DevelopmentTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var header = headerValues.ToString();
        if (!header.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var name = header[HeaderPrefix.Length..].Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = "dev-user";
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, name),
            new Claim(ClaimTypes.Name, name)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
