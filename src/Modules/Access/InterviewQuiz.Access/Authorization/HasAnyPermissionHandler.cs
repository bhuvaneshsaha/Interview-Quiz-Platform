using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace InterviewQuiz.Access.Authorization;

/// <summary>
/// Succeeds when the authenticated principal has at least one of the required permission claims.
/// </summary>
public sealed class HasAnyPermissionHandler : AuthorizationHandler<HasAnyPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasAnyPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && requirement.Permissions.Any(code =>
                context.User.HasClaim(PermissionClaims.Permission, code)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
