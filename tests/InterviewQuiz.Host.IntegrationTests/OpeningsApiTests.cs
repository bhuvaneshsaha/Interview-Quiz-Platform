using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Application.Contracts;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class OpeningsApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public OpeningsApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Anonymous_request_is_unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/openings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Post_without_openings_write_is_forbidden()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.ReaderOnly());

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
        var client = CreateAuthenticatedClient(JwtTestTokens.CreateExpired(PermissionCodes.Openings.Read));

        var response = await client.GetAsync("/api/openings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Wrong_audience_is_unauthorized()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.CreateWrongAudience(PermissionCodes.Openings.Read));

        var response = await client.GetAsync("/api/openings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Post_then_get_opening()
    {
        var client = CreateAuthenticatedClient();

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
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(string? accessToken = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken ?? JwtTestTokens.Recruiter());
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "integration-test");
        return client;
    }
}
