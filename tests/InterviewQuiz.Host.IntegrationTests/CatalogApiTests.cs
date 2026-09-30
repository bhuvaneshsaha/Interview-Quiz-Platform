using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
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

    private static readonly JsonSerializerOptions JsonOptions = CatalogJson.SerializerOptions;

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

    [RequiresDatabaseFact]
    public async Task Get_list_with_keyword_returns_seed_quiz()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Catalog.QuizzesRead));

        var response = await client.GetAsync("/api/quizzes?keyword=Backend&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<QuizResponse>>(JsonOptions);
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == DevelopmentQuizSeeder.SampleQuizBackend);
    }

    [RequiresDatabaseFact]
    public async Task Author_publish_list_get_version_and_clone()
    {
        var client = CreateAuthenticatedClient(AuthorWithTemplates());

        var create = await client.PostAsJsonAsync("/api/quizzes", new
        {
            openingId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            title = "Publish me",
            description = "Slice 3 publish path.",
            expectedExperienceYears = 4,
            tags = new Dictionary<string, string> { ["Role"] = "Backend" },
            questions = new[]
            {
                new
                {
                    type = "trueFalse",
                    stem = "Templates copy questions.",
                    scoringMode = "auto",
                    points = 1,
                    body = new { correct = true },
                    sourceQuestionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var quiz = await create.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        Assert.NotNull(quiz);
        Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), quiz!.Questions[0].SourceQuestionId);

        var publish = await client.PostAsync($"/api/quizzes/{quiz.Id}/publish-template", null);
        Assert.Equal(HttpStatusCode.Created, publish.StatusCode);
        var published = await publish.Content.ReadFromJsonAsync<PublishTemplateResponse>(JsonOptions);
        Assert.NotNull(published);
        Assert.True(published!.CreatedNewTemplate);
        Assert.Equal(1, published.VersionNumber);

        var list = await client.GetAsync("/api/templates?keyword=Publish&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var summaries = await list.Content.ReadFromJsonAsync<PagedResult<TemplateSummaryResponse>>(JsonOptions);
        Assert.NotNull(summaries);
        Assert.Contains(summaries!.Items, item => item.Id == published.TemplateId);

        var get = await client.GetAsync($"/api/templates/{published.TemplateId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var version = await client.GetAsync(
            $"/api/templates/{published.TemplateId}/versions/{published.VersionId}");
        Assert.Equal(HttpStatusCode.OK, version.StatusCode);
        var loaded = await version.Content.ReadFromJsonAsync<TemplateVersionResponse>(JsonOptions);
        Assert.NotNull(loaded);
        Assert.NotEqual(quiz.Questions[0].Id, loaded!.Questions[0].Id);
        Assert.Equal(quiz.Questions[0].SourceQuestionId, loaded.Questions[0].SourceQuestionId);

        var clone = await client.PostAsJsonAsync(
            $"/api/templates/{published.TemplateId}/versions/{published.VersionId}/clone",
            new { openingId = DevelopmentOpeningSeeder.SampleOpeningBackend, title = "Cloned from template" });
        Assert.Equal(HttpStatusCode.Created, clone.StatusCode);
        Assert.Contains("/api/quizzes/", clone.Headers.Location?.ToString() ?? "", StringComparison.Ordinal);
        var cloned = await clone.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        Assert.NotNull(cloned);
        Assert.Equal(published.TemplateId, cloned!.OriginTemplateId);
        Assert.Equal("Cloned from template", cloned.Title);
        Assert.NotEqual(loaded.Questions[0].Id, cloned.Questions[0].Id);
        Assert.Equal(loaded.Questions[0].SourceQuestionId, cloned.Questions[0].SourceQuestionId);
    }

    [RequiresDatabaseFact]
    public async Task Recruiter_with_templates_read_can_list_but_clone_without_quizzes_write_is_forbidden()
    {
        var reader = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Catalog.TemplatesRead,
            PermissionCodes.Catalog.QuizzesRead));

        var list = await reader.GetAsync("/api/templates");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var clone = await reader.PostAsJsonAsync(
            $"/api/templates/{DevelopmentQuizSeeder.SampleTemplateBackend}/versions/{DevelopmentQuizSeeder.SampleTemplateVersion1}/clone",
            new { openingId = DevelopmentOpeningSeeder.SampleOpeningBackend });
        Assert.Equal(HttpStatusCode.Forbidden, clone.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Publish_without_templates_write_is_forbidden()
    {
        var client = CreateAuthenticatedClient(Author());
        var response = await client.PostAsync(
            $"/api/quizzes/{DevelopmentQuizSeeder.SampleQuizBackend}/publish-template",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Seed_template_is_available()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Catalog.TemplatesRead));
        var response = await client.GetAsync($"/api/templates/{DevelopmentQuizSeeder.SampleTemplateBackend}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(JsonOptions);
        Assert.NotNull(template);
        Assert.Equal(DevelopmentQuizSeeder.SampleQuizBackend, template!.OriginQuizId);
        Assert.Equal(1, template.LatestVersionNumber);
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

    private static string AuthorWithTemplates()
        => JwtTestTokens.Create(
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Catalog.TemplatesRead,
            PermissionCodes.Catalog.TemplatesWrite);
}
