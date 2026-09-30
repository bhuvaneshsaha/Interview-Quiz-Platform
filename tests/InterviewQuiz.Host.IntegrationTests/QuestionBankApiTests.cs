using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class QuestionBankApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public QuestionBankApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = CatalogJson.SerializerOptions;

    [RequiresDatabaseFact]
    public async Task Author_crud_list_archive_and_unarchive()
    {
        var client = CreateAuthenticatedClient(Author());

        var create = await client.PostAsJsonAsync("/api/questions", new
        {
            title = "Integration bank TF",
            tags = new Dictionary<string, string> { ["Topic"] = "HTTP" },
            expectedExperienceYears = 2,
            type = "trueFalse",
            stem = "Bank items are copy-on-include.",
            scoringMode = "auto",
            points = 1,
            body = new { correct = true }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<BankQuestionResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Integration bank TF", created!.Title);
        Assert.Null(created.ArchivedAtUtc);

        var get = await client.GetAsync($"/api/questions/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var update = await client.PutAsJsonAsync($"/api/questions/{created.Id}", new
        {
            title = "Integration bank TF updated",
            tags = created.Tags,
            expectedExperienceYears = 2,
            type = "trueFalse",
            stem = created.Stem,
            scoringMode = "auto",
            points = 1,
            body = new { correct = true },
            rowVersion = created.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<BankQuestionResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Integration bank TF updated", updated!.Title);

        var list = await client.GetAsync("/api/questions?keyword=copy-on-include&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<PagedResult<BankQuestionResponse>>(JsonOptions);
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == created.Id);

        var archive = await client.PostAsync($"/api/questions/{created.Id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        var liveList = await client.GetAsync("/api/questions?keyword=copy-on-include&page=1&pageSize=20");
        var livePage = await liveList.Content.ReadFromJsonAsync<PagedResult<BankQuestionResponse>>(JsonOptions);
        Assert.DoesNotContain(livePage!.Items, item => item.Id == created.Id);

        var archivedList = await client.GetAsync("/api/questions?archived=true&keyword=copy-on-include&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, archivedList.StatusCode);
        var archivedPage = await archivedList.Content.ReadFromJsonAsync<PagedResult<BankQuestionResponse>>(JsonOptions);
        Assert.Contains(archivedPage!.Items, item => item.Id == created.Id && item.ArchivedAtUtc is not null);

        var unarchive = await client.PostAsync($"/api/questions/{created.Id}/unarchive", null);
        Assert.Equal(HttpStatusCode.OK, unarchive.StatusCode);

        var restored = await client.GetAsync("/api/questions?keyword=copy-on-include&page=1&pageSize=20");
        var restoredPage = await restored.Content.ReadFromJsonAsync<PagedResult<BankQuestionResponse>>(JsonOptions);
        Assert.Contains(restoredPage!.Items, item => item.Id == created.Id && item.ArchivedAtUtc is null);
    }

    [RequiresDatabaseFact]
    public async Task Seed_bank_items_are_listed()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Catalog.QuestionsRead));
        var response = await client.GetAsync("/api/questions?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<BankQuestionResponse>>(JsonOptions);
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == DevelopmentBankQuestionSeeder.SampleBankMcSingle);
        Assert.Contains(page.Items, item => item.Id == DevelopmentBankQuestionSeeder.SampleBankTrueFalse);
        Assert.Contains(page.Items, item => item.Id == DevelopmentBankQuestionSeeder.SampleBankShortText);
    }

    [RequiresDatabaseFact]
    public async Task Recruiter_write_is_forbidden()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Catalog.QuestionsRead));
        var response = await client.PostAsJsonAsync("/api/questions", new
        {
            title = "Should fail",
            type = "trueFalse",
            stem = "No write.",
            points = 1,
            body = new { correct = true }
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Include_with_both_permissions_copies_sourceQuestionId()
    {
        var client = CreateAuthenticatedClient(Author());
        var quiz = await CreateEmptyQuiz(client);

        var include = await client.PostAsJsonAsync(
            $"/api/quizzes/{quiz.Id}/include-questions",
            new
            {
                questionIds = new[] { DevelopmentBankQuestionSeeder.SampleBankMcSingle },
                rowVersion = quiz.RowVersion
            });
        var includeBody = await include.Content.ReadAsStringAsync();
        Assert.True(include.StatusCode == HttpStatusCode.OK, includeBody);
        var updated = JsonSerializer.Deserialize<QuizResponse>(includeBody, JsonOptions);
        Assert.NotNull(updated);
        Assert.Single(updated!.Questions);
        Assert.NotEqual(DevelopmentBankQuestionSeeder.SampleBankMcSingle, updated.Questions[0].Id);
        Assert.Equal(DevelopmentBankQuestionSeeder.SampleBankMcSingle, updated.Questions[0].SourceQuestionId);
        Assert.Equal(QuestionType.MultipleChoiceSingle, updated.Questions[0].Type);
    }

    [RequiresDatabaseFact]
    public async Task Include_without_questions_read_is_forbidden()
    {
        var writer = CreateAuthenticatedClient(AuthorWithoutQuestionsRead());
        var quiz = await CreateEmptyQuiz(writer);

        var include = await writer.PostAsJsonAsync(
            $"/api/quizzes/{quiz.Id}/include-questions",
            new
            {
                questionIds = new[] { DevelopmentBankQuestionSeeder.SampleBankTrueFalse },
                rowVersion = quiz.RowVersion
            });
        Assert.Equal(HttpStatusCode.Forbidden, include.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task List_archived_without_write_is_forbidden()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Catalog.QuestionsRead));
        var response = await client.GetAsync("/api/questions?archived=true");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Get_unknown_bank_item_is_not_found()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(PermissionCodes.Catalog.QuestionsRead));
        var response = await client.GetAsync($"/api/questions/{Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Include_archived_is_bad_request()
    {
        var client = CreateAuthenticatedClient(Author());
        var created = await client.PostAsJsonAsync("/api/questions", new
        {
            title = "Archive then include",
            type = "trueFalse",
            stem = "Should not be included.",
            points = 1,
            body = new { correct = false }
        });
        var item = await created.Content.ReadFromJsonAsync<BankQuestionResponse>(JsonOptions);
        Assert.NotNull(item);
        var archive = await client.PostAsync($"/api/questions/{item!.Id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        var quiz = await CreateEmptyQuiz(client);
        var include = await client.PostAsJsonAsync(
            $"/api/quizzes/{quiz.Id}/include-questions",
            new { questionIds = new[] { item.Id }, rowVersion = quiz.RowVersion });
        Assert.Equal(HttpStatusCode.BadRequest, include.StatusCode);
        var body = await include.Content.ReadAsStringAsync();
        Assert.Contains("Archived question cannot be included.", body, StringComparison.Ordinal);
    }

    private async Task<QuizResponse> CreateEmptyQuiz(HttpClient client)
    {
        var post = await client.PostAsJsonAsync("/api/quizzes", new
        {
            openingId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            title = "Include target",
            expectedExperienceYears = 1,
            tags = new Dictionary<string, string>(),
            questions = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var quiz = await post.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        Assert.NotNull(quiz);
        return quiz!;
    }

    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "question-bank-integration-test");
        return client;
    }

    private static string Author()
        => JwtTestTokens.Create(
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Catalog.QuestionsRead,
            PermissionCodes.Catalog.QuestionsWrite);

    private static string AuthorWithoutQuestionsRead()
        => JwtTestTokens.Create(
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite);
}
