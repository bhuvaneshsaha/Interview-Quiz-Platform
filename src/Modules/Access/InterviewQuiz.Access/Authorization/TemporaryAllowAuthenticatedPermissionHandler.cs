using Microsoft.AspNetCore.Authorization;

namespace InterviewQuiz.Access.Authorization;

/// <summary>
/// Temporary AuthZ placeholder. Fail closed for anonymous; allow any authenticated identity.
/// Auth will replace this by requiring the permission code on the principal (JWT claims).
/// </summary>
public sealed class TemporaryAllowAuthenticatedPermissionHandler : AuthorizationHandler<HasPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
