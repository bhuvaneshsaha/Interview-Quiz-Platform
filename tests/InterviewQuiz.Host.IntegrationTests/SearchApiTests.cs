using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Search.Application.Contracts;
using InterviewQuiz.Search.Domain;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class SearchApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public SearchApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = CatalogJson.SerializerOptions;

    [RequiresDatabaseFact]
    public async Task Filters_crud_share_and_visibility()
    {
        var owner = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Search.FiltersWrite,
            PermissionCodes.Search.FiltersShare));
        var other = CreateAuthenticatedClient(JwtTestTokens.CreateForUser(
            "other-user",
            PermissionCodes.Search.FiltersWrite,
            PermissionCodes.Search.FiltersShare));

        var createdResponse = await owner.PostAsJsonAsync("/api/filters", new
        {
            name = "Backend quizzes",
            target = "quizzes",
            criteria = new { keyword = "Backend", experienceMinYears = 3 }
        });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<FilterResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(FilterShareMode.Private, created!.ShareMode);
        Assert.Equal("integration-user", created.OwnerUserId);

        var hidden = await other.GetAsync($"/api/filters/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var sharePublic = await owner.PostAsJsonAsync($"/api/filters/{created.Id}/share", new
        {
            shareMode = "publicInsideCompany"
        });
        Assert.Equal(HttpStatusCode.OK, sharePublic.StatusCode);

        var visiblePublic = await other.GetAsync($"/api/filters/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, visiblePublic.StatusCode);

        var hijack = await other.PutAsJsonAsync($"/api/filters/{created.Id}", new
        {
            name = "Stolen",
            target = "quizzes",
            criteria = new { keyword = "Stolen" }
        });
        Assert.Equal(HttpStatusCode.Forbidden, hijack.StatusCode);

        var unshare = await owner.PostAsync($"/api/filters/{created.Id}/unshare", null);
        Assert.Equal(HttpStatusCode.OK, unshare.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/filters/{created.Id}")).StatusCode);

        var shareSpecific = await owner.PostAsJsonAsync($"/api/filters/{created.Id}/share", new
        {
            shareMode = "specificUsers",
            userIds = new[] { "other-user" }
        });
        Assert.Equal(HttpStatusCode.OK, shareSpecific.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/filters/{created.Id}")).StatusCode);

        var list = await other.GetAsync("/api/filters?target=quizzes");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<PagedResult<FilterResponse>>(JsonOptions);
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == created.Id);

        var deleted = await owner.DeleteAsync($"/api/filters/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/filters/{created.Id}")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Create_filter_with_wrong_criteria_shape_is_bad_request()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Search.FiltersWrite));
        var response = await client.PostAsJsonAsync("/api/filters", new
        {
            name = "Wrong shape",
            target = "quizzes",
            criteria = new { owner = "a@example.com", startDateFrom = "2026-10-01" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Create_filter_without_filters_write_is_forbidden()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Catalog.QuizzesRead));
        var response = await client.PostAsJsonAsync("/api/filters", new
        {
            name = "Nope",
            target = "templates",
            criteria = new { keyword = "Backend" }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "search-integration-test");
        return client;
    }
}
