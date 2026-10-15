using System.Security.Claims;
using InterviewQuiz.Kernel.Exceptions;
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

    public static Guid? FindAssignmentId(this ClaimsPrincipal principal)
        => ParseGuidClaim(principal, PermissionClaims.AssignmentId);

    public static Guid? FindAttemptId(this ClaimsPrincipal principal)
        => ParseGuidClaim(principal, PermissionClaims.AttemptId);

    /// <summary>
    /// Candidate JWTs may only touch the assignment in <c>assignment_id</c>. Mismatch is 403, not 404.
    /// </summary>
    public static void EnsureAssignmentScope(this ClaimsPrincipal principal, Guid assignmentId)
    {
        var claimed = principal.FindAssignmentId();
        if (claimed is null || claimed.Value != assignmentId)
        {
            throw new ForbiddenException("This token is not scoped to that assignment.");
        }
    }

    private static Guid? ParseGuidClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
