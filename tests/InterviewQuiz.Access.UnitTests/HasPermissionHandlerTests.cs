using System.Security.Claims;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace InterviewQuiz.Access.UnitTests;

public sealed class HasPermissionHandlerTests
{
    private readonly HasPermissionHandler _handler = new();
    private readonly HasPermissionRequirement _requirement = new(PermissionCodes.Openings.Write);

    [Fact]
    public async Task Succeeds_when_principal_has_permission_claim()
    {
        var user = Principal(authenticated: true, PermissionCodes.Openings.Write);
        var context = new AuthorizationHandlerContext([_requirement], user, resource: null);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Does_not_succeed_when_authenticated_without_permission()
    {
        var user = Principal(authenticated: true, PermissionCodes.Openings.Read);
        var context = new AuthorizationHandlerContext([_requirement], user, resource: null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Candidate_permission_does_not_satisfy_assignments_write()
    {
        var requirement = new HasPermissionRequirement(PermissionCodes.Delivery.AssignmentsWrite);
        var user = Principal(authenticated: true, PermissionCodes.Candidate.AttemptParticipate);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Does_not_succeed_when_anonymous()
    {
        var user = Principal(authenticated: false);
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
