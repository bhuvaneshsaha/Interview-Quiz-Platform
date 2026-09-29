using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

public sealed class OpeningsApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Anonymous_request_is_unauthorized()
    {
        await using var factory = await CreateMigratedFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/openings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Post_without_openings_write_is_forbidden()
    {
        await using var factory = await CreateMigratedFactory();
        var client = CreateAuthenticatedClient(factory, JwtTestTokens.ReaderOnly());

        var response = await client.PostAsJsonAsync("/api/openings", new CreateOpeningRequest
        {
            Title = "Should fail",
            JobDescription = "Missing openings.write.",
            Owner = "tester@example.com",
            StartDate = new DateOnly(2026, 10, 6),
            ExpectedCloseDate = new DateOnly(2026, 12, 6),
            Headcount = 1,
            ExpectedExperienceYears = 2,
            Handlers = ["tester@example.com"],
            Tags = new Dictionary<string, string>()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Expired_token_is_unauthorized()
    {
        await using var factory = await CreateMigratedFactory();
        var client = CreateAuthenticatedClient(factory, JwtTestTokens.CreateExpired(PermissionCodes.Openings.Read));

        var response = await client.GetAsync("/api/openings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Wrong_audience_is_unauthorized()
    {
        await using var factory = await CreateMigratedFactory();
        var client = CreateAuthenticatedClient(
            factory,
            JwtTestTokens.CreateWrongAudience(PermissionCodes.Openings.Read));

        var response = await client.GetAsync("/api/openings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Post_then_get_opening()
    {
        await using var factory = await CreateMigratedFactory();
        var client = CreateAuthenticatedClient(factory);

        var create = new CreateOpeningRequest
        {
            Title = "Integration opening",
            JobDescription = "Created by WebApplicationFactory.",
            Owner = "tester@example.com",
            StartDate = new DateOnly(2026, 10, 6),
            ExpectedCloseDate = new DateOnly(2026, 12, 6),
            Headcount = 1,
            ExpectedExperienceYears = 2,
            Handlers = ["tester@example.com"],
            Tags = new Dictionary<string, string>
            {
                ["Client"] = "Acme",
                ["Project"] = "Phoenix",
                ["Extra"] = "Allowed"
            }
        };

        var post = await client.PostAsJsonAsync("/api/openings", create);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var created = await post.Content.ReadFromJsonAsync<OpeningResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Integration opening", created!.Title);
        Assert.Equal("Allowed", created.Tags["Extra"]);

        var get = await client.GetAsync($"/api/openings/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var loaded = await get.Content.ReadFromJsonAsync<OpeningResponse>(JsonOptions);
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded!.Id);
        Assert.Equal("Acme", loaded.Tags["Client"]);

        var list = await client.GetAsync("/api/openings?owner=tester@example.com&tags=%7B%22Client%22%3A%22Acme%22%7D");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<PagedResult<OpeningResponse>>(JsonOptions);
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == created.Id);
    }

    [RequiresDatabaseFact]
    public async Task Live_health_is_anonymous()
    {
        await using var factory = await CreateMigratedFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(
        InterviewQuizWebApplicationFactory factory,
        string? accessToken = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken ?? JwtTestTokens.Recruiter());
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "integration-test");
        return client;
    }

    private static async Task<InterviewQuizWebApplicationFactory> CreateMigratedFactory()
    {
        var factory = new InterviewQuizWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccessDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<OpeningsDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentAccessSeeder>()
            .SeedAsync();
        return factory;
    }
}
