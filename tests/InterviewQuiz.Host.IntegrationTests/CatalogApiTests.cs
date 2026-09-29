using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class CatalogApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public CatalogApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Post_then_get_quiz()
    {
        var client = CreateAuthenticatedClient(Author());

        var post = await client.PostAsJsonAsync("/api/quizzes", new
        {
            openingId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            title = "Integration quiz",
            description = "Created by WebApplicationFactory.",
            expectedExperienceYears = 4,
            tags = new Dictionary<string, string> { ["Role"] = "Backend" },
            questions = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var created = await post.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Integration quiz", created!.Title);
        Assert.Empty(created.Questions);

        var get = await client.GetAsync($"/api/quizzes/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var loaded = await get.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded!.Id);
    }

    [RequiresDatabaseFact]
    public async Task Post_unknown_opening_is_bad_request()
    {
        var client = CreateAuthenticatedClient(Author());

        var response = await client.PostAsJsonAsync("/api/quizzes", new
        {
            openingId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            title = "Orphan quiz",
            expectedExperienceYears = 1,
            tags = new Dictionary<string, string>(),
            questions = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Opening does not exist.", body, StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Post_mcq_auto_with_zero_correct_is_bad_request()
    {
        var client = CreateAuthenticatedClient(Author());
        var json = """
            {
              "openingId": "3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001",
              "title": "Bad MCQ",
              "expectedExperienceYears": 1,
              "questions": [
                {
                  "type": "multipleChoiceSingle",
                  "stem": "Pick one",
                  "scoringMode": "auto",
                  "points": 1,
                  "body": {
                    "options": [
                      { "id": "a", "text": "One", "isCorrect": false },
                      { "id": "b", "text": "Two", "isCorrect": false }
                    ]
                  }
                }
              ]
            }
            """;

        var response = await client.PostAsync(
            "/api/quizzes",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("exactly one correct", body, StringComparison.OrdinalIgnoreCase);
    }

    [RequiresDatabaseFact]
    public async Task Post_without_quizzes_write_is_forbidden()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Catalog.QuizzesRead));

        var response = await client.PostAsJsonAsync("/api/quizzes", new
        {
            openingId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            title = "Should fail",
            expectedExperienceYears = 1,
            questions = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Get_list_returns_seed_quiz_for_opening()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Catalog.QuizzesRead));

        var response = await client.GetAsync(
            $"/api/quizzes?openingId={DevelopmentOpeningSeeder.SampleOpeningBackend}&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<QuizResponse>>(JsonOptions);
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item =>
            item.OpeningId == DevelopmentOpeningSeeder.SampleOpeningBackend
            && item.Questions.Count >= 8);
    }

    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "catalog-integration-test");
        return client;
    }

    private static string Author()
        => JwtTestTokens.Create(
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite);
}
