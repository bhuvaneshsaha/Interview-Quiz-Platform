using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Evaluation.Domain;
using InterviewQuiz.Host.Composition;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class HumanReviewApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = CatalogJson.SerializerOptions;

    public HumanReviewApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [RequiresDatabaseFact]
    public async Task Reviewer_scores_unsettled_items_and_final_score_replaces_auto_pass()
    {
        var author = Client(JwtTestTokens.Create(
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Evaluation.AttemptsRead,
            PermissionCodes.Evaluation.ReportsRead,
            PermissionCodes.Openings.Read));
        var (quizId, autoId, humanId, assistId) = await CreateMixedQuizAsync(author);
        var email = $"review.{Guid.NewGuid():N}@example.com";
        var assignment = await CreateAssignmentAsync(author, quizId, email);
        var candidate = await CandidateClientAsync(assignment.InviteUrl!);
        Assert.Equal(HttpStatusCode.Created, (await candidate.PostAsync($"/api/assignments/{assignment.Id}/attempts", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(candidate, assignment.Id, autoId, true)).StatusCode);
        var submit = await candidate.PostAsync($"/api/assignments/{assignment.Id}/attempts/current/submit", null);
        var submitted = await submit.Content.ReadFromJsonAsync<CandidateSubmitResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.NotNull(submitted);
        Assert.Equal("incomplete", submitted!.ResultStatus);

        var reader = Client(JwtTestTokens.Create(PermissionCodes.Evaluation.AttemptsRead));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync($"/api/attempts/{submitted.Id}/review")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/attempts/{submitted.Id}/review")).StatusCode);

        var read = await reader.GetAsync($"/api/attempts/{submitted.Id}");
        var readJson = await read.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.DoesNotContain("answerKey", readJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"correct\"", readJson, StringComparison.Ordinal);
        Assert.Contains("Describe the module boundary.", readJson, StringComparison.Ordinal);
        using (var readDoc = JsonDocument.Parse(readJson))
        {
            Assert.False(readDoc.RootElement.TryGetProperty("finalPointsAwarded", out _));
        }

        var reviewer = Client(JwtTestTokens.CreateForUser(
            "reviewer-1",
            PermissionCodes.Evaluation.AttemptsReview,
            PermissionCodes.Evaluation.AttemptsRead,
            PermissionCodes.Delivery.AssignmentsRead));
        var review = await reviewer.GetFromJsonAsync<AttemptReviewResponse>($"/api/attempts/{submitted.Id}/review", JsonOptions);
        Assert.NotNull(review);
        var autoKey = review!.Items.Single(item => item.QuestionId == autoId).AnswerKey;
        Assert.True(autoKey.HasValue);
        Assert.True(autoKey!.Value.GetProperty("correct").GetBoolean());
        Assert.Equal(2, review.Items.Count(item => item.Reviewable));
        Assert.False(review.Items.Single(item => item.QuestionId == autoId).Reviewable);

        var partial = await reviewer.PostAsJsonAsync(
            $"/api/attempts/{submitted.Id}/items/{humanId}/review",
            new ReviewItemRequest { PointsAwarded = 0, Comment = "Missed the boundary." });
        var partialBody = await partial.Content.ReadFromJsonAsync<AttemptReviewResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        Assert.Equal("incomplete", partialBody!.ResultStatus);
        Assert.Null(partialBody.FinalPointsAwarded);
        Assert.Equal("reviewer-1", partialBody.Items.Single(item => item.QuestionId == humanId).ReviewedByUserId);

        var pending = await author.GetFromJsonAsync<AssignmentResponse>($"/api/assignments/{assignment.Id}", JsonOptions);
        Assert.Equal("pendingReview", pending!.Status);

        var autoDenied = await reviewer.PostAsJsonAsync(
            $"/api/attempts/{submitted.Id}/items/{autoId}/review",
            new ReviewItemRequest { PointsAwarded = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, autoDenied.StatusCode);
        Assert.Contains(Attempt.NotReviewableMessage, await autoDenied.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var rangeDenied = await reviewer.PostAsJsonAsync(
            $"/api/attempts/{submitted.Id}/items/{assistId}/review",
            new ReviewItemRequest { PointsAwarded = 10.0001m });
        Assert.Equal(HttpStatusCode.BadRequest, rangeDenied.StatusCode);

        var final = await reviewer.PostAsJsonAsync(
            $"/api/attempts/{submitted.Id}/items/{assistId}/review",
            new ReviewItemRequest { PointsAwarded = 0 });
        var finalBody = await final.Content.ReadFromJsonAsync<AttemptReviewResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, final.StatusCode);
        Assert.Equal("complete", finalBody!.ResultStatus);
        Assert.Equal(10m, finalBody.FinalPointsAwarded);
        Assert.Equal(30m, finalBody.TotalPointsAvailable);
        Assert.True(finalBody.Items.Single(item => item.QuestionId == autoId).AnswerKey!.Value.GetProperty("correct").GetBoolean());

        var completed = await author.GetFromJsonAsync<AssignmentResponse>($"/api/assignments/{assignment.Id}", JsonOptions);
        Assert.Equal("completed", completed!.Status);

        var stillRedacted = await reader.GetAsync($"/api/attempts/{submitted.Id}");
        var redactedJson = await stillRedacted.Content.ReadAsStringAsync();
        Assert.DoesNotContain("answerKey", redactedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"correct\"", redactedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Missed the boundary.", redactedJson, StringComparison.Ordinal);

        var scores = await GetScoresAsync(author, quizId);
        var row = scores.Items.Single(item => item.CandidateEmail == email);
        Assert.Equal("final", row.ScoreBasis);
        Assert.Equal(10m, row.FinalPointsAwarded);
        Assert.False(row.Passed);
        Assert.Equal(0, row.UnsettledCount);
        Assert.Equal("completed", row.Status);
    }

    private async Task<ScoresReportResponse> GetScoresAsync(HttpClient client, Guid quizId)
    {
        var criteria = JsonSerializer.Serialize(new
        {
            filters = new[] { new { field = "quizId", @operator = "is", value = quizId.ToString() } }
        }, JsonOptions);
        var response = await client.GetAsync($"/api/reports/scores?criteria={Uri.EscapeDataString(criteria)}&pageSize=100");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<ScoresReportResponse>(body, JsonOptions)!;
    }

    private async Task<(Guid QuizId, Guid AutoId, Guid HumanId, Guid AssistId)> CreateMixedQuizAsync(HttpClient client)
    {
        var json = $$"""
            {
              "openingId": "{{DevelopmentOpeningSeeder.SampleOpeningBackend}}",
              "title": "Human review quiz {{Guid.NewGuid():N}}",
              "expectedExperienceYears": 1,
              "questions": [
                {
                  "type": "trueFalse",
                  "stem": "A bounded context owns its schema.",
                  "scoringMode": "auto",
                  "points": 10,
                  "body": { "correct": true }
                },
                {
                  "type": "longText",
                  "stem": "Describe the module boundary.",
                  "scoringMode": "humanOnly",
                  "points": 10,
                  "body": { "guidance": "Name the public API." }
                },
                {
                  "type": "longText",
                  "stem": "Where should a reviewer confirm an AI suggestion?",
                  "scoringMode": "aiAssist",
                  "points": 10,
                  "body": { "guidance": "On the review screen." }
                }
              ]
            }
            """;
        var response = await client.PostAsync("/api/quizzes", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        var quiz = await client.GetFromJsonAsync<QuizResponse>($"/api/quizzes/{created!.Id}", JsonOptions);
        return (
            quiz!.Id,
            quiz.Questions.Single(question => question.Type == QuestionType.TrueFalse).Id,
            quiz.Questions.Single(question => question.ScoringMode == ScoringMode.HumanOnly).Id,
            quiz.Questions.Single(question => question.ScoringMode == ScoringMode.AiAssist).Id);
    }

    private static async Task<AssignmentResponse> CreateAssignmentAsync(HttpClient client, Guid quizId, string email)
    {
        var response = await client.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = email,
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions))!;
    }

    private async Task<HttpClient> CandidateClientAsync(string inviteUrl)
    {
        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = MagicLinkApiTests.TokenFromInviteUrl(inviteUrl) });
        Assert.Equal(HttpStatusCode.OK, consume.StatusCode);
        var candidate = await consume.Content.ReadFromJsonAsync<CandidateTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candidate!.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid assignmentId, Guid questionId, bool value)
        => client.PutAsJsonAsync(
            $"/api/assignments/{assignmentId}/attempts/current/answers",
            new SaveAnswersRequest
            {
                Answers =
                [
                    new AnswerDto
                    {
                        QuestionId = questionId,
                        Value = JsonSerializer.SerializeToElement(new { value }, JsonOptions)
                    }
                ]
            });

    private HttpClient Client(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
