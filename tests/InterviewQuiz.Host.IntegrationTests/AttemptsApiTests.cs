using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class AttemptsApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public AttemptsApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Consume_start_save_submit_auto_quiz_completes_without_keys_on_results()
    {
        var (assignment, questionId) = await CreateAutoAssignmentAsync();
        var token = MagicLinkApiTests.TokenFromInviteUrl(assignment.InviteUrl!);

        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = token });
        Assert.Equal(HttpStatusCode.OK, consume.StatusCode);
        var candidate = await consume.Content.ReadFromJsonAsync<CandidateTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", candidate!.AccessToken);

        var start = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var started = await start.Content.ReadFromJsonAsync<CandidateAttemptResponse>(JsonOptions);
        Assert.NotNull(started);
        Assert.Equal("inProgress", started!.Status);
        Assert.DoesNotContain("isCorrect", await start.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"correct\"", await start.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var save = await client.PutAsJsonAsync(
            $"/api/assignments/{assignment.Id}/attempts/current/answers",
            new SaveAnswersRequest
            {
                Answers =
                [
                    new AnswerDto
                    {
                        QuestionId = questionId,
                        Value = JsonSerializer.SerializeToElement(new { value = true }, JsonOptions)
                    }
                ]
            });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var submit = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts/current/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var submitted = await submit.Content.ReadFromJsonAsync<CandidateSubmitResponse>(JsonOptions);
        Assert.NotNull(submitted);
        Assert.Equal("submitted", submitted!.Status);
        Assert.Equal("complete", submitted.ResultStatus);
        Assert.Equal(1m, submitted.AutoPointsAwarded);

        var recruiter = CreateAuthenticatedClient(RecruiterWithAttempts());
        var assignmentGet = await recruiter.GetFromJsonAsync<AssignmentResponse>(
            $"/api/assignments/{assignment.Id}",
            JsonOptions);
        Assert.Equal("completed", assignmentGet!.Status);

        var list = await recruiter.GetFromJsonAsync<PagedResult<AttemptSummaryResponse>>(
            $"/api/assignments/{assignment.Id}/attempts",
            JsonOptions);
        Assert.NotNull(list);
        Assert.Single(list!.Items);

        var result = await recruiter.GetAsync($"/api/attempts/{submitted.Id}");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var body = await result.Content.ReadAsStringAsync();
        Assert.DoesNotContain("isCorrect", body, StringComparison.Ordinal);
        Assert.DoesNotContain("acceptableAnswers", body, StringComparison.Ordinal);
        var detail = JsonSerializer.Deserialize<AttemptResultResponse>(body, JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal("complete", detail!.ResultStatus);
        Assert.Equal(1m, detail.AutoPointsAwarded);
        Assert.NotNull(detail.Items[0].Stem);
    }

    [RequiresDatabaseFact]
    public async Task Employee_jwt_cannot_post_attempts()
    {
        var (assignment, _) = await CreateAutoAssignmentAsync();
        var client = CreateAuthenticatedClient(RecruiterWithAttempts());
        var response = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Live_assignment_start_is_bad_request()
    {
        var recruiter = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite));
        var quizId = await CreateAutoQuizAsync(recruiter);
        var post = await recruiter.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = $"live.{Guid.NewGuid():N}@example.com",
            Mode = "live"
        });
        var assignment = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.Null(assignment!.InviteUrl);

        using var scope = _factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IJwtAccessTokenIssuer>();
        var candidateJwt = issuer.IssueCandidate(
            "live-candidate",
            "live.candidate@example.com",
            assignment.Id,
            attemptId: null);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", candidateJwt.Token);
        var start = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
        Assert.Contains(
            "Assignment is live; start is not available.",
            await start.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Candidate_mismatched_assignment_id_is_forbidden()
    {
        var (assignment, _) = await CreateAutoAssignmentAsync();
        var other = await CreateAutoAssignmentAsync();
        var token = MagicLinkApiTests.TokenFromInviteUrl(assignment.InviteUrl!);
        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = token });
        var candidate = await consume.Content.ReadFromJsonAsync<CandidateTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", candidate!.AccessToken);

        var start = await client.PostAsync($"/api/assignments/{other.Assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
    }

    private async Task<(AssignmentResponse Assignment, Guid QuestionId)> CreateAutoAssignmentAsync()
    {
        var recruiter = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Evaluation.AttemptsRead));
        var quizId = await CreateAutoQuizAsync(recruiter);
        var quiz = await recruiter.GetFromJsonAsync<QuizResponse>($"/api/quizzes/{quizId}", JsonOptions);
        var questionId = quiz!.Questions[0].Id;

        var post = await recruiter.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = $"auto.{Guid.NewGuid():N}@example.com",
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 }
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var assignment = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(assignment);
        return (assignment!, questionId);
    }

    private static async Task<Guid> CreateAutoQuizAsync(HttpClient client)
    {
        var json = $$"""
            {
              "openingId": "{{DevelopmentOpeningSeeder.SampleOpeningBackend}}",
              "title": "Auto-only slice 5 quiz",
              "expectedExperienceYears": 1,
              "questions": [
                {
                  "type": "trueFalse",
                  "stem": "A quiz belongs to exactly one opening.",
                  "scoringMode": "auto",
                  "points": 1,
                  "body": { "correct": true }
                }
              ]
            }
            """;
        var response = await client.PostAsync(
            "/api/quizzes",
            new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        return created!.Id;
    }

    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static string RecruiterWithAttempts()
        => JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Evaluation.AttemptsRead);
}
