using System.Security.Claims;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace InterviewQuiz.Access.UnitTests;

public sealed class HasAnyPermissionHandlerTests
{
    private readonly HasAnyPermissionHandler _handler = new();
    private readonly HasAnyPermissionRequirement _requirement = new(
        [PermissionCodes.Catalog.QuizzesRead, PermissionCodes.Catalog.QuizzesWrite]);

    [Fact]
    public async Task Succeeds_when_principal_has_either_permission()
    {
        var user = Principal(authenticated: true, PermissionCodes.Catalog.QuizzesWrite);
        var context = new AuthorizationHandlerContext([_requirement], user, resource: null);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Does_not_succeed_when_authenticated_without_any_listed_permission()
    {
        var user = Principal(authenticated: true, PermissionCodes.Openings.Read);
        var context = new AuthorizationHandlerContext([_requirement], user, resource: null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static ClaimsPrincipal Principal(bool authenticated, params string[] permissions)
    {
        if (!authenticated)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = permissions.Select(code => new Claim(PermissionClaims.Permission, code));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "jwt"));
    }
}
