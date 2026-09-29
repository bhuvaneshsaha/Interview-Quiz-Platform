using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Host.Hosting;

/// <summary>
/// Placeholder scheme until Auth wires JWT bearer. Always fails (deny by default).
/// </summary>
public sealed class UnconfiguredAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AuthNotConfigured";

    public UnconfiguredAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.Fail(
            "Authentication is not configured. Auth must wire JWT bearer before Production use."));
}
