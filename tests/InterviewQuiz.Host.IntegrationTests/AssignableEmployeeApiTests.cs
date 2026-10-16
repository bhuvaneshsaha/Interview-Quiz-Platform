using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Application.Services;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class AssignableEmployeeApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public AssignableEmployeeApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Assignable_search_returns_only_active_employees()
    {
        var sample = await SeedDirectoryAsync();
        var client = CreateClient();

        Assert.Empty(await SearchEmailsAsync(client, sample.CandidateEmail));
        Assert.Empty(await SearchEmailsAsync(client, sample.RolelessEmail));
        Assert.Empty(await SearchEmailsAsync(client, sample.DisabledEmail));
        Assert.Empty(await SearchEmailsAsync(client, sample.PasswordlessRoleEmail));
        Assert.Contains(sample.EmployeeEmail, await SearchEmailsAsync(client, sample.EmployeeEmail));
        Assert.Contains(
            DevelopmentAccessSeeder.RecruiterEmail,
            await SearchEmailsAsync(client, DevelopmentAccessSeeder.RecruiterEmail));
    }

    [RequiresDatabaseFact]
    public async Task Create_and_update_reject_non_employee_owner_and_handlers()
    {
        var sample = await SeedDirectoryAsync();
        var client = CreateClient();
        var employeeId = await FindAssignableUserIdAsync(client, sample.EmployeeEmail);
        Assert.Equal(sample.EmployeeId, employeeId);

        var candidateOwner = await PostOpeningAsync(client, sample.CandidateId, [employeeId]);
        Assert.Equal(HttpStatusCode.BadRequest, candidateOwner.StatusCode);
        var candidateOwnerBody = await candidateOwner.Content.ReadAsStringAsync();
        Assert.Contains(OpeningService.OwnerNotEmployeeMessage, candidateOwnerBody, StringComparison.Ordinal);
        Assert.DoesNotContain(OpeningService.HandlersNotEmployeeMessage, candidateOwnerBody, StringComparison.Ordinal);

        var rolelessHandler = await PostOpeningAsync(client, employeeId, [employeeId, sample.RolelessId]);
        Assert.Equal(HttpStatusCode.BadRequest, rolelessHandler.StatusCode);
        var rolelessBody = await rolelessHandler.Content.ReadAsStringAsync();
        Assert.Contains(OpeningService.HandlersNotEmployeeMessage, rolelessBody, StringComparison.Ordinal);
        Assert.DoesNotContain(OpeningService.OwnerNotEmployeeMessage, rolelessBody, StringComparison.Ordinal);

        var disabledOwner = await PostOpeningAsync(client, sample.DisabledId, []);
        Assert.Equal(HttpStatusCode.BadRequest, disabledOwner.StatusCode);
        Assert.Contains(
            OpeningService.OwnerNotEmployeeMessage,
            await disabledOwner.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var passwordlessOwner = await PostOpeningAsync(client, sample.PasswordlessRoleId, []);
        Assert.Equal(HttpStatusCode.BadRequest, passwordlessOwner.StatusCode);
        Assert.Contains(
            OpeningService.OwnerNotEmployeeMessage,
            await passwordlessOwner.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var created = await PostOpeningAsync(client, employeeId, [employeeId]);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var opening = await created.Content.ReadFromJsonAsync<OpeningResponse>(JsonOptions);
        Assert.NotNull(opening);

        var candidateHandler = await PutOpeningAsync(client, opening!, sample.EmployeeId, [sample.CandidateId]);
        Assert.Equal(HttpStatusCode.BadRequest, candidateHandler.StatusCode);
        var candidateHandlerBody = await candidateHandler.Content.ReadAsStringAsync();
        Assert.Contains(OpeningService.HandlersNotEmployeeMessage, candidateHandlerBody, StringComparison.Ordinal);
        Assert.DoesNotContain(OpeningService.OwnerNotEmployeeMessage, candidateHandlerBody, StringComparison.Ordinal);

        var rolelessOwner = await PutOpeningAsync(client, opening!, sample.RolelessId, [employeeId]);
        Assert.Equal(HttpStatusCode.BadRequest, rolelessOwner.StatusCode);
        var rolelessOwnerBody = await rolelessOwner.Content.ReadAsStringAsync();
        Assert.Contains(OpeningService.OwnerNotEmployeeMessage, rolelessOwnerBody, StringComparison.Ordinal);
        Assert.DoesNotContain(OpeningService.HandlersNotEmployeeMessage, rolelessOwnerBody, StringComparison.Ordinal);
    }

    private async Task<DirectorySample> SeedDirectoryAsync()
    {
        _factory.Clock.Unfreeze();
        var suffix = Guid.NewGuid().ToString("N");
        var employeeEmail = $"employee.{suffix}@example.com";
        var candidateEmail = $"candidate.{suffix}@example.com";
        var rolelessEmail = $"roleless.{suffix}@example.com";
        var disabledEmail = $"disabled.{suffix}@example.com";
        var passwordlessEmail = $"passwordless.{suffix}@example.com";

        var employeeId = await CreatePasswordUserAsync(employeeEmail, DevelopmentAccessSeeder.RecruiterRoleId, disabled: false);
        var rolelessId = await CreatePasswordUserAsync(rolelessEmail, roleId: null, disabled: false);
        var disabledId = await CreatePasswordUserAsync(disabledEmail, DevelopmentAccessSeeder.RecruiterRoleId, disabled: true);
        var passwordlessId = await CreatePasswordlessRoleHolderAsync(passwordlessEmail, DevelopmentAccessSeeder.ReviewerRoleId);
        var candidateId = await CreateCandidateViaMagicLinkAsync(candidateEmail);
        await AssertCandidateOnlyAsync(candidateId);

        var stillEmployeeId = await CreateCandidateViaMagicLinkAsync(employeeEmail);
        Assert.Equal(employeeId, stillEmployeeId);

        return new DirectorySample(
            employeeId,
            employeeEmail,
            candidateId,
            candidateEmail,
            rolelessId,
            rolelessEmail,
            disabledId,
            disabledEmail,
            passwordlessId,
            passwordlessEmail);
    }

    private async Task<string> CreateCandidateViaMagicLinkAsync(string email)
    {
        var client = CreateClient();
        var post = await client.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = DevelopmentQuizSeeder.SampleQuizBackend,
            CandidateEmail = email,
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 },
            AttemptLimit = 1
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(created?.InviteUrl);

        var consume = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = MagicLinkApiTests.TokenFromInviteUrl(created!.InviteUrl!) });
        Assert.Equal(HttpStatusCode.OK, consume.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user!.Id;
    }

    private async Task AssertCandidateOnlyAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        Assert.True(string.IsNullOrEmpty(user!.PasswordHash));
        Assert.False(user.IsDisabled);
        Assert.False(await db.UserRoles.AnyAsync(assignment => assignment.UserId == userId));
    }

    private async Task<string> CreatePasswordUserAsync(string email, Guid? roleId, bool disabled)
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

        if (roleId is Guid assignedRole)
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            db.UserRoles.Add(new UserRoleAssignment { UserId = user.Id, RoleId = assignedRole });
            await db.SaveChangesAsync();
        }

        if (disabled)
        {
            user.IsDisabled = true;
            var updated = await users.UpdateAsync(user);
            Assert.True(updated.Succeeded, string.Join(" ", updated.Errors.Select(error => error.Description)));
        }

        return user.Id;
    }

    private async Task<string> CreatePasswordlessRoleHolderAsync(string email, Guid roleId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsDisabled = false,
            MustSetPassword = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var created = await users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));
        Assert.True(string.IsNullOrEmpty(user.PasswordHash));

        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        db.UserRoles.Add(new UserRoleAssignment { UserId = user.Id, RoleId = roleId });
        await db.SaveChangesAsync();
        Assert.True(await db.UserRoles.AnyAsync(assignment => assignment.UserId == user.Id));
        return user.Id;
    }

    private async Task<HttpResponseMessage> PostOpeningAsync(HttpClient client, string ownerId, IReadOnlyList<string> handlers)
    {
        return await client.PostAsJsonAsync("/api/openings", new CreateOpeningRequest
        {
            Title = "Employee picker opening",
            JobDescription = "Owner and handlers must be active employees.",
            Owner = ownerId,
            StartDate = new DateOnly(2026, 10, 6),
            ExpectedCloseDate = new DateOnly(2026, 12, 6),
            Headcount = 1,
            ExpectedExperienceYears = 2,
            Handlers = handlers.ToList(),
            Tags = new Dictionary<string, string>()
        });
    }

    private async Task<HttpResponseMessage> PutOpeningAsync(
        HttpClient client,
        OpeningResponse opening,
        string ownerId,
        IReadOnlyList<string> handlers)
    {
        return await client.PutAsJsonAsync($"/api/openings/{opening.Id}", new UpdateOpeningRequest
        {
            Id = opening.Id,
            Title = opening.Title,
            JobDescription = opening.JobDescription,
            Owner = ownerId,
            StartDate = opening.StartDate,
            ExpectedCloseDate = opening.ExpectedCloseDate,
            Headcount = opening.Headcount,
            ExpectedExperienceYears = opening.ExpectedExperienceYears,
            Handlers = handlers.ToList(),
            Tags = opening.Tags.ToDictionary(pair => pair.Key, pair => pair.Value),
            RowVersion = opening.RowVersion
        });
    }

    private static async Task<IReadOnlyList<string>> SearchEmailsAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/users/assignable?q={Uri.EscapeDataString(query)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("email").GetString() ?? "")
            .ToArray();
    }

    private static async Task<string> FindAssignableUserIdAsync(HttpClient client, string email)
    {
        var response = await client.GetAsync($"/api/users/assignable?q={Uri.EscapeDataString(email)}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var match = document.RootElement.EnumerateArray().Single(item =>
            string.Equals(item.GetProperty("email").GetString(), email, StringComparison.OrdinalIgnoreCase));
        return match.GetProperty("id").GetString()!;
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokens.Create(
                PermissionCodes.Openings.Read,
                PermissionCodes.Openings.Write,
                PermissionCodes.Delivery.AssignmentsRead,
                PermissionCodes.Delivery.AssignmentsWrite));
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "assignable-employee-test");
        return client;
    }

    private sealed record DirectorySample(
        string EmployeeId,
        string EmployeeEmail,
        string CandidateId,
        string CandidateEmail,
        string RolelessId,
        string RolelessEmail,
        string DisabledId,
        string DisabledEmail,
        string PasswordlessRoleId,
        string PasswordlessRoleEmail);
}
