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

        var ownerId = await FindAssignableUserIdAsync(client, "recruiter.dev@example.com");
        var create = new CreateOpeningRequest
        {
            Title = "Integration opening",
            JobDescription = "Created by WebApplicationFactory.",
            Owner = ownerId,
            StartDate = new DateOnly(2026, 10, 6),
            ExpectedCloseDate = new DateOnly(2026, 12, 6),
            Headcount = 1,
            ExpectedExperienceYears = 2,
            Handlers = [ownerId],
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
        Assert.Equal(ownerId, created.Owner);
        Assert.True(created.OwnerResolved);
        Assert.False(string.IsNullOrWhiteSpace(created.OwnerName));
        Assert.Equal("recruiter.dev@example.com", created.OwnerEmail);

        var get = await client.GetAsync($"/api/openings/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var loaded = await get.Content.ReadFromJsonAsync<OpeningResponse>(JsonOptions);
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded!.Id);
        Assert.Equal("Acme", loaded.Tags["Client"]);

        var list = await client.GetAsync(
            "/api/openings?owner=recruiter.dev@example.com&tags=%7B%22Client%22%3A%22Acme%22%7D");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<PagedResult<OpeningResponse>>(JsonOptions);
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == created.Id);
        Assert.Contains(page.Items, item => item.Id == created.Id && item.OwnerName == created.OwnerName);
    }

    [RequiresDatabaseFact]
    public async Task Post_unknown_owner_is_bad_request()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/openings", new CreateOpeningRequest
        {
            Title = "Unknown owner",
            JobDescription = "Should fail validation.",
            Owner = "not-a-user",
            StartDate = new DateOnly(2026, 10, 6),
            Headcount = 1,
            ExpectedExperienceYears = 1,
            Handlers = []
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Assignable_users_search_returns_id_name_and_email()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/users/assignable?q=author.dev@example.com");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var match = document.RootElement.EnumerateArray().Single(item =>
            item.GetProperty("email").GetString() == "author.dev@example.com");
        Assert.False(string.IsNullOrWhiteSpace(match.GetProperty("id").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(match.GetProperty("name").GetString()));
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
