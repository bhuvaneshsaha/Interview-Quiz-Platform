using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Migrations;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class EmployeeReportsReadApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly InterviewQuizWebApplicationFactory _factory;

    public EmployeeReportsReadApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [RequiresDatabaseFact]
    public async Task Seeded_employees_can_open_reports_and_a_candidate_is_forbidden()
    {
        (string Email, string Password)[] employees =
        [
            (DevelopmentAccessSeeder.AdminEmail, DevelopmentAccessSeeder.AdminPassword),
            (DevelopmentAccessSeeder.AuthorEmail, DevelopmentAccessSeeder.AuthorPassword),
            (DevelopmentAccessSeeder.ReviewerEmail, DevelopmentAccessSeeder.ReviewerPassword),
            (DevelopmentAccessSeeder.RecruiterEmail, DevelopmentAccessSeeder.RecruiterPassword)
        ];

        foreach (var (email, password) in employees)
        {
            var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
            Assert.NotNull(tokens);

            var issued = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken).Claims
                .Where(claim => claim.Type == PermissionClaims.Permission)
                .Select(claim => claim.Value)
                .ToArray();
            Assert.Contains(PermissionCodes.Evaluation.ReportsRead, issued);
            Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, issued);

            var client = Client(tokens.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/funnel")).StatusCode);
            var me = await client.GetFromJsonAsync<MeResponse>("/api/me", JsonOptions);
            Assert.NotNull(me);
            Assert.Contains(PermissionCodes.Evaluation.ReportsRead, me.Permissions);
        }

        using var scope = _factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IJwtAccessTokenIssuer>();
        var candidate = issuer.IssueCandidate(
            $"candidate-{Guid.NewGuid():N}",
            $"candidate-{Guid.NewGuid():N}@example.com",
            Guid.NewGuid(),
            attemptId: null);
        var candidateClient = Client(candidate.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await candidateClient.GetAsync("/api/reports/funnel")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await candidateClient.GetAsync("/api/reports/scores")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Grant_adds_reports_read_to_an_employee_role_and_skips_a_candidate_role()
    {
        var employeeRoleId = Guid.NewGuid();
        var candidateRoleId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        db.Roles.Add(new AccessRole
        {
            Id = employeeRoleId,
            Name = $"Employee bundle {employeeRoleId:N}",
            Description = "Existing employee role missing reports.read."
        });
        db.Roles.Add(new AccessRole
        {
            Id = candidateRoleId,
            Name = $"Candidate bundle {candidateRoleId:N}",
            Description = "Holds candidate attempt access."
        });
        db.RolePermissions.Add(new RolePermission
        {
            RoleId = employeeRoleId,
            PermissionCode = PermissionCodes.Openings.Read
        });
        db.RolePermissions.Add(new RolePermission
        {
            RoleId = candidateRoleId,
            PermissionCode = PermissionCodes.Candidate.AttemptParticipate
        });
        await db.SaveChangesAsync();

        try
        {
            await db.Database.ExecuteSqlRawAsync(ReportsReadEmployeeGrant.ApplySql);

            Assert.True(await HasReportsReadAsync(db, employeeRoleId));
            Assert.False(await HasReportsReadAsync(db, candidateRoleId));
            Assert.True(await HasReportsReadAsync(db, SuperAdminRole.Id));
            Assert.True(await HasReportsReadAsync(db, DevelopmentAccessSeeder.AdminRoleId));
            Assert.True(await HasReportsReadAsync(db, DevelopmentAccessSeeder.TemplateAuthorRoleId));
            Assert.True(await HasReportsReadAsync(db, DevelopmentAccessSeeder.ReviewerRoleId));
            Assert.True(await HasReportsReadAsync(db, DevelopmentAccessSeeder.RecruiterRoleId));
        }
        finally
        {
            var permissions = await db.RolePermissions
                .Where(permission => permission.RoleId == employeeRoleId || permission.RoleId == candidateRoleId)
                .ToListAsync();
            db.RolePermissions.RemoveRange(permissions);
            var roles = await db.Roles
                .Where(role => role.Id == employeeRoleId || role.Id == candidateRoleId)
                .ToListAsync();
            db.Roles.RemoveRange(roles);
            await db.SaveChangesAsync();
        }
    }

    private static Task<bool> HasReportsReadAsync(AccessDbContext db, Guid roleId)
        => db.RolePermissions.AsNoTracking().AnyAsync(permission =>
            permission.RoleId == roleId && permission.PermissionCode == PermissionCodes.Evaluation.ReportsRead);

    private HttpClient Client(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
