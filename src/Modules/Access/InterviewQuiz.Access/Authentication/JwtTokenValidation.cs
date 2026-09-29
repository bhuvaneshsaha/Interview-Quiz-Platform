using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace InterviewQuiz.Access.Authentication;

public static class JwtTokenValidation
{
    public static TokenValidationParameters Create(JwtOptions jwt)
    {
        ArgumentNullException.ThrowIfNull(jwt);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "email",
            RoleClaimType = "role"
        };
    }

    public static SigningCredentials CreateSigningCredentials(JwtOptions jwt)
    {
        ArgumentNullException.ThrowIfNull(jwt);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }
}
