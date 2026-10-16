using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class UserDirectoryApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly InterviewQuizWebApplicationFactory _factory;

    public UserDirectoryApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [RequiresDatabaseFact]
    public async Task Staff_list_hides_invite_only_people_until_asked_and_shows_role_names()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var staffEmail = $"00-staff-{suffix}@example.com";
        var candidateEmail = $"00-candidate-{suffix}@example.com";
        var rolelessEmail = $"00-roleless-{suffix}@example.com";

        await CreateStaffAsync(staffEmail, DevelopmentAccessSeeder.RecruiterRoleId);
        await CreateCandidateAsync(candidateEmail);
        await CreateStaffAsync(rolelessEmail, roleId: null);

        var client = CreateClient();
        var staffOnly = await FindAsync(client, staffEmail, includeCandidates: false);
        var hiddenCandidate = await FindAsync(client, candidateEmail, includeCandidates: false);
        var roleless = await FindAsync(client, rolelessEmail, includeCandidates: false);

        Assert.NotNull(staffOnly);
        Assert.False(staffOnly!.IsCandidate);
        Assert.Contains("Dev Recruiter", staffOnly.RoleNames);
        Assert.Null(hiddenCandidate);
        Assert.NotNull(roleless);
        Assert.False(roleless!.IsCandidate);
        Assert.Empty(roleless.RoleNames);

        var candidate = await FindAsync(client, candidateEmail, includeCandidates: true);
        Assert.NotNull(candidate);
        Assert.True(candidate!.IsCandidate);
        Assert.Empty(candidate.RoleNames);
        Assert.Equal(candidateEmail, candidate.Email);
    }

    private async Task CreateStaffAsync(string email, Guid? roleId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsDisabled = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var created = await users.CreateAsync(user, "Employee.Pass!1");
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));

        if (roleId is Guid assigned)
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            db.UserRoles.Add(new UserRoleAssignment { UserId = user.Id, RoleId = assigned });
            await db.SaveChangesAsync();
        }
    }

    private async Task CreateCandidateAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsDisabled = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var created = await users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));
        Assert.True(string.IsNullOrEmpty(user.PasswordHash));
    }

    private static async Task<UserResponse?> FindAsync(HttpClient client, string email, bool includeCandidates)
    {
        for (var page = 1; page <= 30; page++)
        {
            var flag = includeCandidates ? "true" : "false";
            var response = await client.GetAsync($"/api/users?page={page}&pageSize=100&includeCandidates={flag}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PagedUsersResponse>(JsonOptions);
            Assert.NotNull(body);
            var match = body!.Items.FirstOrDefault(user =>
                string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            if (body.Items.Count == 0 || page * body.PageSize >= body.TotalCount)
            {
                return null;
            }
        }

        return null;
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokens.Create(PermissionCodes.Access.UsersManage));
        return client;
    }
}
