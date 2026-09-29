using System.Security.Claims;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace InterviewQuiz.Access.Authentication;

public static class AccessClaimsPrincipalExtensions
{
    public static string? FindUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
    }

    public static string? FindEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");
    }

    public static IReadOnlyList<string> FindPermissions(this ClaimsPrincipal principal)
    {
        return principal
            .FindAll(PermissionClaims.Permission)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
