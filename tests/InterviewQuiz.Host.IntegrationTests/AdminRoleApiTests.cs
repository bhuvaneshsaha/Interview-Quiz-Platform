using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class AdminRoleApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly InterviewQuizWebApplicationFactory _factory;

    public AdminRoleApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [RequiresDatabaseFact]
    public async Task Admin_can_manage_users_roles_and_reports_but_cannot_assign_super_admin()
    {
        Assert.True(AdminRole.Description.Length <= 512);

        var login = await LoginAsync(DevelopmentAccessSeeder.AdminEmail, DevelopmentAccessSeeder.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(tokens);

        var issued = PermissionClaimsOf(tokens!.AccessToken);
        Assert.Contains(PermissionCodes.Access.UsersManage, issued);
        Assert.Contains(PermissionCodes.Access.RolesManage, issued);
        Assert.Contains(PermissionCodes.Evaluation.ReportsRead, issued);
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, issued);

        var client = Client(tokens.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", JsonOptions);
        Assert.NotNull(me);
        Assert.Contains(PermissionCodes.Access.UsersManage, me!.Permissions);
        Assert.Contains(PermissionCodes.Access.RolesManage, me.Permissions);
        Assert.Contains(PermissionCodes.Evaluation.ReportsRead, me.Permissions);
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, me.Permissions);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/funnel")).StatusCode);

        var created = await client.PostAsJsonAsync("/api/users", new
        {
            email = $"admin-made-{Guid.NewGuid():N}@example.com",
            password = "Admin.Made!1"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = await created.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.NotNull(user);

        var roles = await client.GetFromJsonAsync<List<RoleResponse>>("/api/roles", JsonOptions);
        var admin = roles!.Single(role => role.Id == AdminRole.Id);
        Assert.Equal(AdminRole.Name, admin.Name);
        Assert.Equal(AdminRole.Description, admin.Description);
        Assert.True(admin.IsBuiltIn);
        Assert.False(admin.IsSystem);
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, admin.PermissionCodes);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            var catalog = await db.Permissions.AsNoTracking().Select(permission => permission.Code).ToListAsync();
            var expected = SuperAdminPermissionSet.Codes()
                .Where(code => catalog.Contains(code, StringComparer.Ordinal))
                .ToArray();
            Assert.Equal(expected, admin.PermissionCodes);
        }

        var assign = await client.PostAsJsonAsync(
            $"/api/roles/{SuperAdminRole.Id}/users",
            new { userId = user!.Id });
        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
        Assert.Contains(SuperAdminRole.AssignForbiddenMessage, await assign.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var reserved = await client.PostAsJsonAsync("/api/roles", new
        {
            name = AdminRole.Name,
            description = "copy",
            permissionCodes = new[] { PermissionCodes.Access.RolesManage }
        });
        Assert.Equal(HttpStatusCode.BadRequest, reserved.StatusCode);
        Assert.Contains(AdminRole.ReservedNameMessage, await reserved.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Admin_permission_edits_stick_and_the_role_cannot_be_deleted()
    {
        var login = await LoginAsync(DevelopmentAccessSeeder.AdminEmail, DevelopmentAccessSeeder.AdminPassword);
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        var client = Client(tokens!.AccessToken);
        var before = await client.GetFromJsonAsync<RoleResponse>($"/api/roles/{AdminRole.Id}", JsonOptions);
        Assert.NotNull(before);
        var original = before!.PermissionCodes.ToArray();
        Assert.Contains(PermissionCodes.Openings.Write, original);

        try
        {
            var removed = original
                .Where(code => !string.Equals(code, PermissionCodes.Openings.Write, StringComparison.Ordinal))
                .ToArray();
            var updated = await client.PutAsJsonAsync($"/api/roles/{AdminRole.Id}", new
            {
                name = "Renamed Admin",
                description = "changed by the editor",
                permissionCodes = removed
            });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            var body = await updated.Content.ReadFromJsonAsync<RoleResponse>(JsonOptions);
            Assert.NotNull(body);
            Assert.Equal(AdminRole.Name, body!.Name);
            Assert.Equal(AdminRole.Description, body.Description);
            Assert.True(body.IsBuiltIn);
            Assert.False(body.IsSystem);
            Assert.DoesNotContain(PermissionCodes.Openings.Write, body.PermissionCodes);
            Assert.Contains(PermissionCodes.Access.RolesManage, body.PermissionCodes);

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = AdminRole.Id,
                    PermissionCode = PermissionCodes.Candidate.AttemptParticipate
                });
                await db.SaveChangesAsync();
            }

            await EnsureAsync();

            var after = await client.GetFromJsonAsync<RoleResponse>($"/api/roles/{AdminRole.Id}", JsonOptions);
            Assert.NotNull(after);
            Assert.Equal(AdminRole.Name, after!.Name);
            Assert.DoesNotContain(PermissionCodes.Openings.Write, after.PermissionCodes);
            Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, after.PermissionCodes);
            Assert.Contains(PermissionCodes.Access.UsersManage, after.PermissionCodes);

            var delete = await client.DeleteAsync($"/api/roles/{AdminRole.Id}");
            Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
            Assert.Contains(AdminRole.DeleteForbiddenMessage, await delete.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally
        {
            var restore = await client.PutAsJsonAsync($"/api/roles/{AdminRole.Id}", new
            {
                name = AdminRole.Name,
                description = AdminRole.Description,
                permissionCodes = original
            });
            Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        }
    }

    [RequiresDatabaseFact]
    public async Task Startup_folds_a_legacy_dev_admin_and_keeps_a_single_admin_role()
    {
        var holder = await CreateUserAsync($"legacy-admin-{Guid.NewGuid():N}@example.com");
        Guid legacyRoleId = Guid.Empty;
        Guid customRoleId = Guid.Empty;

        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
                var admin = await db.Roles.SingleAsync(role => role.Id == AdminRole.Id);
                admin.Name = "Admin parked";

                customRoleId = Guid.NewGuid();
                db.Roles.Add(new AccessRole
                {
                    Id = customRoleId,
                    Name = AdminRole.Name,
                    Description = "operator copy",
                    IsSystem = false,
                    IsBuiltIn = false
                });

                legacyRoleId = Guid.NewGuid();
                var legacy = new AccessRole
                {
                    Id = legacyRoleId,
                    Name = AdminRole.LegacyName,
                    Description = "old development seed",
                    IsSystem = false,
                    IsBuiltIn = false,
                    Permissions =
                    [
                        new RolePermission
                        {
                            RoleId = legacyRoleId,
                            PermissionCode = PermissionCodes.Openings.Read
                        }
                    ]
                };
                legacy.UserRoles.Add(new UserRoleAssignment { UserId = holder, RoleId = legacyRoleId });
                db.Roles.Add(legacy);
                await db.SaveChangesAsync();
            }

            await EnsureAsync();

            using var check = _factory.Services.CreateScope();
            var saved = check.ServiceProvider.GetRequiredService<AccessDbContext>();
            Assert.False(await saved.Roles.AnyAsync(role => role.Id == legacyRoleId));
            Assert.False(await saved.Roles.AnyAsync(role => role.Name == AdminRole.LegacyName));
            var builtIn = await saved.Roles.Include(role => role.Permissions).SingleAsync(role => role.Id == AdminRole.Id);
            Assert.Equal(AdminRole.Name, builtIn.Name);
            Assert.Equal(AdminRole.Description, builtIn.Description);
            Assert.True(builtIn.IsBuiltIn);
            Assert.False(builtIn.IsSystem);
            Assert.Contains(builtIn.Permissions, permission => permission.PermissionCode == PermissionCodes.Access.UsersManage);
            Assert.DoesNotContain(builtIn.Permissions, permission =>
                permission.PermissionCode == PermissionCodes.Candidate.AttemptParticipate);
            Assert.True(await saved.UserRoles.AnyAsync(assignment =>
                assignment.UserId == holder && assignment.RoleId == AdminRole.Id));
            var namedAdmin = await saved.Roles.SingleAsync(role => role.Name == AdminRole.Name);
            Assert.Equal(AdminRole.Id, namedAdmin.Id);
            var custom = await saved.Roles.SingleAsync(role => role.Id == customRoleId);
            Assert.StartsWith("Admin (custom)", custom.Name, StringComparison.Ordinal);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            var extras = await db.Roles
                .Where(role => role.Id == legacyRoleId || role.Id == customRoleId)
                .ToListAsync();
            if (extras.Count > 0)
            {
                db.Roles.RemoveRange(extras);
                await db.SaveChangesAsync();
            }
        }

        await EnsureAsync();
    }

    private static string[] PermissionClaimsOf(string accessToken)
        => new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims
            .Where(claim => claim.Type == PermissionClaims.Permission)
            .Select(claim => claim.Value)
            .ToArray();

    private async Task EnsureAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SuperAdminBootstrap>().EnsureAsync();
    }

    private async Task<string> CreateUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var created = await users.CreateAsync(user, "Legacy.Admin!1");
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));
        return user.Id;
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
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "admin-role-integration");
        return client;
    }
}
