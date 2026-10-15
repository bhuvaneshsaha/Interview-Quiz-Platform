using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class AiDraftApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = CatalogJson.SerializerOptions;

    public AiDraftApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [RequiresDatabaseFact]
    public async Task Draft_requires_an_edit_then_saves_a_quiz_without_an_assignment()
    {
        var author = Client(JwtTestTokens.CreateForUser(
            "ai-draft-author",
            PermissionCodes.Catalog.AiDraftUse,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Delivery.AssignmentsRead));
        var denied = Client(JwtTestTokens.Create(PermissionCodes.Catalog.QuizzesWrite));
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/ai-drafts", new { openingId = DevelopmentOpeningSeeder.SampleOpeningBackend })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsJsonAsync("/api/ai-drafts", new { openingId = DevelopmentOpeningSeeder.SampleOpeningBackend })).StatusCode);

        var missing = await author.PostAsJsonAsync("/api/ai-drafts", new { openingId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("Opening does not exist.", await missing.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var created = await author.PostAsJsonAsync("/api/ai-drafts", new
        {
            openingId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            resumeText = "Ten years of APIs.",
            rulesText = "Prefer short stems."
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<AiDraftResponse>(JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal("fake", draft!.Model);
        Assert.Equal("generated", draft.Status);
        Assert.Equal("Ten years of APIs.", draft.ResumeText);
        Assert.Equal("Draft written answer from the resume.", draft.Questions[1].Stem);
        Assert.Equal("Prefer short stems.", draft.Questions[1].Body.GetProperty("guidance").GetString());
        Assert.Null(draft.AcceptedQuizId);

        var other = Client(JwtTestTokens.CreateForUser("ai-draft-other", PermissionCodes.Catalog.AiDraftUse));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/ai-drafts/{draft.Id}")).StatusCode);

        var loaded = await author.GetFromJsonAsync<AiDraftResponse>($"/api/ai-drafts/{draft.Id}", JsonOptions);
        Assert.Equal(draft.Id, loaded!.Id);

        var unchanged = await author.PostAsJsonAsync($"/api/ai-drafts/{draft.Id}/accept", Accept(draft, draft.Questions[0].Stem));
        Assert.Equal(HttpStatusCode.BadRequest, unchanged.StatusCode);
        Assert.Contains(AiDraft.EditRequiredMessage, await unchanged.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var assignmentsBefore = await AssignmentCountAsync(author);
        var accepted = await author.PostAsJsonAsync(
            $"/api/ai-drafts/{draft.Id}/accept",
            Accept(draft, draft.Questions[0].Stem + " Edited."));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var saved = await accepted.Content.ReadFromJsonAsync<AiDraftAcceptResponse>(JsonOptions);
        Assert.NotNull(saved);
        Assert.Equal(draft.Id, saved!.DraftId);

        var quiz = await author.GetFromJsonAsync<QuizResponse>($"/api/quizzes/{saved.QuizId}", JsonOptions);
        Assert.NotNull(quiz);
        Assert.Equal(DevelopmentOpeningSeeder.SampleOpeningBackend, quiz!.OpeningId);
        Assert.Contains("Edited.", quiz.Questions[0].Stem, StringComparison.Ordinal);
        Assert.Equal(0, quiz.AssignmentCount);
        Assert.Equal(assignmentsBefore, await AssignmentCountAsync(author));

        var again = await author.PostAsJsonAsync(
            $"/api/ai-drafts/{draft.Id}/accept",
            Accept(draft, draft.Questions[0].Stem + " Edited again."));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains(AiDraft.AlreadyAcceptedMessage, await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var writerOnly = Client(JwtTestTokens.CreateForUser(
            "ai-draft-author",
            PermissionCodes.Catalog.AiDraftUse));
        var blocked = await writerOnly.PostAsJsonAsync(
            $"/api/ai-drafts/{draft.Id}/accept",
            Accept(draft, "Different stem."));
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    private static object Accept(AiDraftResponse draft, string firstStem)
        => new
        {
            title = draft.Title,
            description = "Saved from the draft.",
            expectedExperienceYears = draft.ExpectedExperienceYears,
            questions = draft.Questions.Select((question, index) => new
            {
                type = CatalogJson.ToCamelCase(question.Type),
                stem = index == 0 ? firstStem : question.Stem,
                scoringMode = CatalogJson.ToCamelCase(question.ScoringMode),
                points = question.Points,
                body = JsonSerializer.Deserialize<JsonElement>(question.Body.GetRawText(), JsonOptions)
            })
        };

    private async Task<int> AssignmentCountAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/assignments?pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("totalCount").GetInt32();
    }

    private HttpClient Client(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
