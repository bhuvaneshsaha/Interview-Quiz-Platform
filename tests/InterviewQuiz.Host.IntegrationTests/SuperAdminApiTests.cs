using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class SuperAdminApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public SuperAdminApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task System_role_is_immutable_and_tokens_ignore_a_stale_permission_row()
    {
        var holder = await CreateUserAsync($"super-{Guid.NewGuid():N}@example.com", "Holder.Pass!1");
        var other = await CreateUserAsync($"other-{Guid.NewGuid():N}@example.com", "Other.Pass!1");
        await AssignSuperAdminAsync(holder.Id);
        await AssignSuperAdminAsync(other.Id);
        await LeaveOnlyTheseHoldersAsync(holder.Id, other.Id);
        await RemoveRolePermissionAsync(PermissionCodes.Catalog.QuizzesWrite);

        var login = await LoginAsync(holder.Email, "Holder.Pass!1");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(tokens);
        var holderClient = Client(tokens!.AccessToken);

        var me = await holderClient.GetFromJsonAsync<List<string>>("/api/me/permissions", JsonOptions);
        Assert.NotNull(me);
        Assert.Contains(PermissionCodes.Catalog.QuizzesWrite, me);
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, me);

        var rename = await holderClient.PutAsJsonAsync($"/api/roles/{SuperAdminRole.Id}", new
        {
            name = "Not Super Admin",
            description = "nope",
            permissionCodes = new[] { PermissionCodes.Access.RolesManage }
        });
        Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
        Assert.Contains(SuperAdminRole.ImmutableMessage, await rename.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var delete = await holderClient.DeleteAsync($"/api/roles/{SuperAdminRole.Id}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        var outsider = Client(JwtTestTokens.Create(PermissionCodes.Access.RolesManage));
        var forbidden = await outsider.PostAsJsonAsync(
            $"/api/roles/{SuperAdminRole.Id}/users",
            new { userId = other.Id });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Contains(SuperAdminRole.AssignForbiddenMessage, await forbidden.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var unassignOther = await holderClient.DeleteAsync($"/api/roles/{SuperAdminRole.Id}/users/{other.Id}");
        Assert.Equal(HttpStatusCode.NoContent, unassignOther.StatusCode);

        var unassignLast = await holderClient.DeleteAsync($"/api/roles/{SuperAdminRole.Id}/users/{holder.Id}");
        Assert.Equal(HttpStatusCode.Conflict, unassignLast.StatusCode);
        Assert.Contains(SuperAdminRole.LastHolderUnassignMessage, await unassignLast.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var deactivate = await holderClient.PutAsJsonAsync($"/api/users/{holder.Id}", new { isDisabled = true });
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Contains(SuperAdminRole.LastHolderDeactivateMessage, await deactivate.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var audit = await db.AuditEvents.AsNoTracking()
            .SingleAsync(item => item.Action == SuperAdminRole.RevokedAction && item.TargetUserId == other.Id);
        Assert.Equal(holder.Id, audit.ActorUserId);
        Assert.False(string.IsNullOrWhiteSpace(audit.CorrelationId));
        Assert.NotEqual(default, audit.OccurredAtUtc);

        using (var resync = _factory.Services.CreateScope())
        {
            await resync.ServiceProvider.GetRequiredService<SuperAdminBootstrap>().EnsureAsync();
        }

        var roles = await holderClient.GetFromJsonAsync<List<RoleResponse>>("/api/roles", JsonOptions);
        var super = roles!.Single(role => role.Id == SuperAdminRole.Id);
        Assert.Contains(PermissionCodes.Catalog.QuizzesWrite, super.PermissionCodes);
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, super.PermissionCodes);
    }

    [RequiresDatabaseFact]
    public async Task Bootstrap_set_password_then_login_and_role_stays_system()
    {
        var email = $"bootstrap-{Guid.NewGuid():N}@example.com";
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                MustSetPassword = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var created = await users.CreateAsync(user);
            Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));
            token = scope.ServiceProvider.GetRequiredService<IBootstrapPasswordService>().IssueToken(user);
            var updated = await users.UpdateAsync(user);
            Assert.True(updated.Succeeded, string.Join(" ", updated.Errors.Select(error => error.Description)));
        }

        var client = _factory.CreateClient();
        var blocked = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Not.Set!1" });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains(SuperAdminRole.PasswordSetupRequiredMessage, await blocked.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var set = await client.PostAsJsonAsync("/api/auth/bootstrap/set-password", new
        {
            email,
            token,
            newPassword = "Set.Once!1"
        });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var replay = await client.PostAsJsonAsync("/api/auth/bootstrap/set-password", new
        {
            email,
            token,
            newPassword = "Set.Twice!1"
        });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Set.Once!1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var admin = await LoginAsync(DevelopmentAccessSeeder.AdminEmail, DevelopmentAccessSeeder.AdminPassword);
        var adminTokens = await admin.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        var roles = await Client(adminTokens!.AccessToken).GetFromJsonAsync<List<RoleResponse>>("/api/roles", JsonOptions);
        var super = roles!.Single(role => role.Id == SuperAdminRole.Id);
        Assert.True(super.IsSystem);
        Assert.Equal(SuperAdminRole.Name, super.Name);
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, super.PermissionCodes);
        Assert.Contains(PermissionCodes.Access.UsersManage, super.PermissionCodes);
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        return await client.PostAsJsonAsync("/api/auth/login", new { email, password });
    }

    private HttpClient Client(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "super-admin-integration");
        return client;
    }

    private async Task<(string Id, string Email)> CreateUserAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var created = await users.CreateAsync(user, password);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));
        return (user.Id, email);
    }

    private async Task AssignSuperAdminAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var exists = await db.UserRoles.AnyAsync(assignment =>
            assignment.UserId == userId && assignment.RoleId == SuperAdminRole.Id);
        if (!exists)
        {
            db.UserRoles.Add(new UserRoleAssignment { UserId = userId, RoleId = SuperAdminRole.Id });
            await db.SaveChangesAsync();
        }
    }

    private async Task LeaveOnlyTheseHoldersAsync(params string[] userIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var extras = await db.UserRoles
            .Where(assignment => assignment.RoleId == SuperAdminRole.Id && !userIds.Contains(assignment.UserId))
            .ToListAsync();
        if (extras.Count == 0)
        {
            return;
        }

        db.UserRoles.RemoveRange(extras);
        await db.SaveChangesAsync();
    }

    private async Task RemoveRolePermissionAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var row = await db.RolePermissions.SingleOrDefaultAsync(permission =>
            permission.RoleId == SuperAdminRole.Id && permission.PermissionCode == code);
        if (row is not null)
        {
            db.RolePermissions.Remove(row);
            await db.SaveChangesAsync();
        }
    }
}
