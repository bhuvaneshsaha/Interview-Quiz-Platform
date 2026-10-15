using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InterviewQuiz.Access.Authentication;

public sealed class JwtAccessTokenIssuer : IJwtAccessTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtAccessTokenIssuer(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public IssuedAccessToken Issue(string userId, string email, IEnumerable<string> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(permissions);

        return Create(userId, email, permissions, extraClaims: [], _options.AccessTokenMinutes);
    }

    public IssuedAccessToken IssueCandidate(string userId, string email, Guid assignmentId, Guid? attemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var extra = new List<Claim>
        {
            new(PermissionClaims.AssignmentId, assignmentId.ToString("D"))
        };
        if (attemptId is { } id)
        {
            extra.Add(new Claim(PermissionClaims.AttemptId, id.ToString("D")));
        }

        return Create(
            userId,
            email,
            [PermissionCodes.Candidate.AttemptParticipate],
            extra,
            _options.CandidateAccessTokenMinutes);
    }

    private IssuedAccessToken Create(
        string userId,
        string email,
        IEnumerable<string> permissions,
        IReadOnlyList<Claim> extraClaims,
        int lifetimeMinutes)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(lifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email)
        };

        foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                claims.Add(new Claim(PermissionClaims.Permission, permission));
            }
        }

        claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: JwtTokenValidation.CreateSigningCredentials(_options));

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedAccessToken(encoded, expires);
    }
}
