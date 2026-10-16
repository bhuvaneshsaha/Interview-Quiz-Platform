using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Scoring;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
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

    private static readonly JsonSerializerOptions JsonOptions = CatalogJson.SerializerOptions;

    [RequiresDatabaseFact]
    public async Task Consume_start_save_submit_auto_quiz_completes_without_keys_on_results()
    {
        var (assignment, questionId) = await CreateAutoAssignmentAsync(showScoreToCandidate: true);
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
        var startJson = await start.Content.ReadAsStringAsync();
        Assert.DoesNotContain("isCorrect", startJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"correct\"", startJson, StringComparison.Ordinal);
        AssertCandidatePayloadOmitsWeights(startJson);
        Assert.Contains("\"type\"", startJson, StringComparison.Ordinal);

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
            Mode = "live",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 }
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
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
            "Waiting for the host to start.",
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

    [RequiresDatabaseFact]
    public async Task Consume_leaves_no_attempt_and_start_sets_due_once()
    {
        var recruiter = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Evaluation.AttemptsRead));
        var quizId = await CreateAutoQuizAsync(recruiter);
        var post = await recruiter.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = $"welcome.{Guid.NewGuid():N}@example.com",
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 45 }
        });
        var assignment = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(assignment);
        var token = MagicLinkApiTests.TokenFromInviteUrl(assignment!.InviteUrl!);

        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = token });
        Assert.Equal(HttpStatusCode.OK, consume.StatusCode);
        var candidate = await consume.Content.ReadFromJsonAsync<CandidateTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", candidate!.AccessToken);

        var before = await recruiter.GetFromJsonAsync<PagedResult<AttemptSummaryResponse>>(
            $"/api/assignments/{assignment.Id}/attempts",
            JsonOptions);
        Assert.NotNull(before);
        Assert.Empty(before!.Items);

        var preview = await client.GetAsync($"/api/assignments/{assignment.Id}/attempts/preview");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewJson = await preview.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"questions\"", previewJson, StringComparison.Ordinal);
        Assert.DoesNotContain("dueAtUtc", previewJson, StringComparison.Ordinal);
        using (var previewDoc = JsonDocument.Parse(previewJson))
        {
            Assert.Equal("notStarted", previewDoc.RootElement.GetProperty("attemptStatus").GetString());
            Assert.Equal(45, previewDoc.RootElement.GetProperty("durationMinutes").GetInt32());
            Assert.Equal(1, previewDoc.RootElement.GetProperty("questionCount").GetInt32());
            Assert.Equal("Auto-only slice 5 quiz", previewDoc.RootElement.GetProperty("quizTitle").GetString());
            Assert.Equal("Senior backend engineer", previewDoc.RootElement.GetProperty("openingName").GetString());
        }

        var missing = await client.GetAsync($"/api/assignments/{assignment.Id}/attempts/current");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var start = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var started = await start.Content.ReadFromJsonAsync<CandidateAttemptResponse>(JsonOptions);
        Assert.NotNull(started);
        Assert.Equal(started!.StartedAtUtc.AddMinutes(45), started.DueAtUtc);
        AssertCandidatePayloadOmitsWeights(await start.Content.ReadAsStringAsync());

        var second = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var again = await second.Content.ReadFromJsonAsync<CandidateAttemptResponse>(JsonOptions);
        Assert.Equal(started.Id, again!.Id);
        Assert.Equal(started.DueAtUtc, again.DueAtUtc);

        var list = await recruiter.GetFromJsonAsync<PagedResult<AttemptSummaryResponse>>(
            $"/api/assignments/{assignment.Id}/attempts",
            JsonOptions);
        Assert.Single(list!.Items);
    }

    [RequiresDatabaseFact]
    public async Task Assigned_snapshot_stays_immutable_after_source_quiz_is_edited()
    {
        var recruiter = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Evaluation.AttemptsRead));

        var createJson = $$"""
            {
              "openingId": "{{DevelopmentOpeningSeeder.SampleOpeningBackend}}",
              "title": "Snapshot freeze quiz",
              "expectedExperienceYears": 1,
              "questions": [
                {
                  "type": "trueFalse",
                  "stem": "Frozen stem: a quiz belongs to one opening.",
                  "scoringMode": "auto",
                  "points": 1,
                  "body": { "correct": true }
                },
                {
                  "type": "multipleChoiceSingle",
                  "stem": "Frozen choice stem.",
                  "scoringMode": "auto",
                  "points": 1,
                  "body": {
                    "options": [
                      { "id": "opt-old", "text": "Frozen option text", "isCorrect": true },
                      { "id": "opt-other", "text": "Other frozen text", "isCorrect": false }
                    ]
                  }
                }
              ]
            }
            """;
        var createdResponse = await recruiter.PostAsync(
            "/api/quizzes",
            new StringContent(createJson, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var quiz = await createdResponse.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        Assert.NotNull(quiz);

        var assignmentResponse = await recruiter.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quiz!.Id,
            CandidateEmail = $"freeze.{Guid.NewGuid():N}@example.com",
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 20 },
            ShowScoreToCandidate = true
        });
        var assignment = await assignmentResponse.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(assignment);
        Assert.Equal(quiz.RowVersion, assignment!.SnapshotQuizRowVersion);

        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = MagicLinkApiTests.TokenFromInviteUrl(assignment.InviteUrl!) });
        var candidate = await consume.Content.ReadFromJsonAsync<CandidateTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", candidate!.AccessToken);

        var startedBeforeEdit = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.Created, startedBeforeEdit.StatusCode);
        var inProgress = await startedBeforeEdit.Content.ReadAsStringAsync();
        Assert.Contains("Frozen stem: a quiz belongs to one opening.", inProgress, StringComparison.Ordinal);

        var trueFalse = quiz.Questions.Single(q => q.Type == QuestionType.TrueFalse);
        var choice = quiz.Questions.Single(q => q.Type == QuestionType.MultipleChoiceSingle);
        var updateJson = $$"""
            {
              "id": "{{quiz.Id}}",
              "openingId": "{{DevelopmentOpeningSeeder.SampleOpeningBackend}}",
              "title": "Snapshot freeze quiz",
              "expectedExperienceYears": 1,
              "rowVersion": {{quiz.RowVersion}},
              "questions": [
                {
                  "id": "{{trueFalse.Id}}",
                  "type": "trueFalse",
                  "stem": "Edited stem: the live quiz changed.",
                  "scoringMode": "auto",
                  "points": 4,
                  "body": { "correct": false }
                },
                {
                  "id": "{{choice.Id}}",
                  "type": "multipleChoiceSingle",
                  "stem": "Edited choice stem.",
                  "scoringMode": "auto",
                  "points": 9,
                  "body": {
                    "options": [
                      { "id": "opt-old", "text": "Edited option text", "isCorrect": false },
                      { "id": "opt-new", "text": "Brand new option", "isCorrect": true }
                    ]
                  }
                }
              ]
            }
            """;
        var updatedResponse = await recruiter.PutAsync(
            $"/api/quizzes/{quiz.Id}",
            new StringContent(updateJson, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var edited = await recruiter.GetFromJsonAsync<QuizResponse>($"/api/quizzes/{quiz.Id}", JsonOptions);
        Assert.Equal("Edited stem: the live quiz changed.", edited!.Questions.Single(q => q.Id == trueFalse.Id).Stem);
        Assert.Equal(trueFalse.Id, edited.Questions.Single(q => q.Type == QuestionType.TrueFalse).Id);
        Assert.Equal(choice.Id, edited.Questions.Single(q => q.Type == QuestionType.MultipleChoiceSingle).Id);
        Assert.NotEqual(quiz.RowVersion, edited.RowVersion);
        Assert.True(edited.AssignmentCount >= 1);

        var frozenAssignment = await recruiter.GetFromJsonAsync<AssignmentResponse>(
            $"/api/assignments/{assignment.Id}",
            JsonOptions);
        Assert.Equal(quiz.RowVersion, frozenAssignment!.SnapshotQuizRowVersion);

        var current = await client.GetAsync($"/api/assignments/{assignment.Id}/attempts/current");
        var currentJson = await current.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Contains("Frozen stem: a quiz belongs to one opening.", currentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Edited stem", currentJson, StringComparison.Ordinal);

        var start = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        var startJson = await start.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Contains("Frozen stem: a quiz belongs to one opening.", startJson, StringComparison.Ordinal);
        Assert.Contains("Frozen option text", startJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Edited stem", startJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Edited option text", startJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Brand new option", startJson, StringComparison.Ordinal);
        Assert.DoesNotContain("isCorrect", startJson, StringComparison.Ordinal);
        AssertCandidatePayloadOmitsWeights(startJson);

        var save = await client.PutAsJsonAsync(
            $"/api/assignments/{assignment.Id}/attempts/current/answers",
            new SaveAnswersRequest
            {
                Answers =
                [
                    new AnswerDto
                    {
                        QuestionId = trueFalse.Id,
                        Value = JsonSerializer.SerializeToElement(new { value = true }, JsonOptions)
                    },
                    new AnswerDto
                    {
                        QuestionId = choice.Id,
                        Value = JsonSerializer.SerializeToElement(new { optionId = "opt-old" }, JsonOptions)
                    }
                ]
            });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var submit = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts/current/submit", null);
        var submitted = await submit.Content.ReadFromJsonAsync<CandidateSubmitResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal(2m, submitted!.AutoPointsAwarded);
        Assert.Equal(2m, submitted.AutoPointsAvailable);

        var result = await recruiter.GetAsync($"/api/attempts/{submitted.Id}");
        var resultJson = await result.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("Frozen stem: a quiz belongs to one opening.", resultJson, StringComparison.Ordinal);
        Assert.Contains("Frozen option text", resultJson, StringComparison.Ordinal);
        Assert.DoesNotContain("isCorrect", resultJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Edited stem", resultJson, StringComparison.Ordinal);
        using (var resultDoc = JsonDocument.Parse(resultJson))
        {
            Assert.Equal((long)quiz.RowVersion, resultDoc.RootElement.GetProperty("quizRowVersion").GetInt64());
        }
    }

    private static void AssertCandidatePayloadOmitsWeights(string json)
    {
        using var document = JsonDocument.Parse(json);
        Walk(document.RootElement);

        static void Walk(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        Assert.False(
                            property.Name is "points" or "scoringMode" or "creditMode",
                            $"Candidate payload included '{property.Name}'.");
                        Walk(property.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item);
                    }
                    break;
            }
        }
    }

    [RequiresDatabaseFact]
    public async Task Candidate_preview_redacts_the_quiz_and_persists_nothing()
    {
        var recruiter = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite));
        var quizId = await CreateAutoQuizAsync(recruiter);

        var preview = await recruiter.GetAsync($"/api/quizzes/{quizId}/candidate-preview");
        var previewJson = await preview.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.DoesNotContain("isCorrect", previewJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"correct\"", previewJson, StringComparison.Ordinal);
        Assert.DoesNotContain("points", previewJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A quiz belongs to exactly one opening.", previewJson, StringComparison.Ordinal);

        var assignments = await recruiter.GetFromJsonAsync<PagedResult<AssignmentSummaryResponse>>(
            $"/api/assignments?quizId={quizId}",
            JsonOptions);
        Assert.NotNull(assignments);
        Assert.Empty(assignments!.Items);

        var score = await recruiter.PostAsJsonAsync(
            $"/api/quizzes/{quizId}/candidate-preview/score",
            new PreviewScoreRequest { Answers = [] });
        Assert.Equal(HttpStatusCode.OK, score.StatusCode);
        var scored = await score.Content.ReadFromJsonAsync<CandidatePreviewScoreResponse>(JsonOptions);
        Assert.Equal(0m, scored!.AutoPointsAwarded);
        Assert.Equal(1m, scored.AutoPointsAvailable);
    }

    [RequiresDatabaseFact]
    public async Task Revoke_ends_an_in_progress_attempt_and_blocks_further_answers()
    {
        var (assignment, questionId) = await CreateAutoAssignmentAsync();
        var client = await CandidateClientAsync(assignment.InviteUrl!);
        var start = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);

        var save = await SaveTrueAsync(client, assignment.Id, questionId);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var recruiter = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Evaluation.AttemptsRead));
        var revoke = await recruiter.PostAsync($"/api/assignments/{assignment.Id}/invite/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var blockedSave = await SaveTrueAsync(client, assignment.Id, questionId);
        Assert.Equal(HttpStatusCode.BadRequest, blockedSave.StatusCode);
        Assert.Contains(InviteMessages.Revoked, await blockedSave.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var current = await client.GetAsync($"/api/assignments/{assignment.Id}/attempts/current");
        Assert.Equal(HttpStatusCode.BadRequest, current.StatusCode);
        Assert.Contains(InviteMessages.Revoked, await current.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var submit = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts/current/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);

        var assignmentGet = await recruiter.GetFromJsonAsync<AssignmentResponse>(
            $"/api/assignments/{assignment.Id}",
            JsonOptions);
        Assert.NotEqual("inProgress", assignmentGet!.Status);
        Assert.NotEqual("notStarted", assignmentGet.Status);

        var list = await recruiter.GetFromJsonAsync<PagedResult<AttemptSummaryResponse>>(
            $"/api/assignments/{assignment.Id}/attempts",
            JsonOptions);
        Assert.Equal("submitted", Assert.Single(list!.Items).Status);
    }

    [RequiresDatabaseFact]
    public async Task Expiry_does_not_stop_an_in_progress_attempt()
    {
        var (assignment, questionId) = await CreateAutoAssignmentAsync();
        var token = MagicLinkApiTests.TokenFromInviteUrl(assignment.InviteUrl!);
        var client = await CandidateClientAsync(assignment.InviteUrl!);
        var start = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            var invite = await db.MagicLinkInvites.SingleAsync(row =>
                row.AssignmentId == assignment.Id && row.RevokedAt == null);
            invite.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var save = await SaveTrueAsync(client, assignment.Id, questionId);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = await save.Content.ReadFromJsonAsync<CandidateAttemptResponse>(JsonOptions);
        Assert.Equal("inProgress", saved!.Status);

        var current = await client.GetAsync($"/api/assignments/{assignment.Id}/attempts/current");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);

        var anonymous = _factory.CreateClient();
        var consume = await anonymous.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = token });
        Assert.Equal(HttpStatusCode.BadRequest, consume.StatusCode);
        Assert.Contains(InviteMessages.Expired, await consume.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private async Task<HttpClient> CandidateClientAsync(string inviteUrl)
    {
        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = MagicLinkApiTests.TokenFromInviteUrl(inviteUrl) });
        Assert.Equal(HttpStatusCode.OK, consume.StatusCode);
        var candidate = await consume.Content.ReadFromJsonAsync<CandidateTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", candidate!.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> SaveTrueAsync(HttpClient client, Guid assignmentId, Guid questionId)
        => client.PutAsJsonAsync(
            $"/api/assignments/{assignmentId}/attempts/current/answers",
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

    [RequiresDatabaseFact]
    public async Task Shuffled_attempt_stays_stable_and_recruiter_detail_stays_canonical()
    {
        var recruiter = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Evaluation.AttemptsRead));
        var quizId = await CreateShuffledQuizAsync(recruiter);
        var quiz = await recruiter.GetFromJsonAsync<QuizResponse>($"/api/quizzes/{quizId}", JsonOptions);
        Assert.NotNull(quiz);
        Assert.True(quiz!.ShuffleQuestions);
        Assert.True(quiz.ShuffleOptions);
        var canonicalIds = quiz.Questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id).Select(q => q.Id).ToArray();
        var scoredQuestion = quiz.Questions.Single(q => q.Stem == "Pick the right status.");

        string? firstOrder = null;
        HttpClient? firstClient = null;
        AssignmentResponse? firstAssignment = null;
        CandidateAttemptResponse? firstAttempt = null;
        var orders = new List<string>();
        var optionOrders = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            var assignment = await AssignAsync(recruiter, quizId);
            var client = await CandidateClientAsync(assignment.InviteUrl!);
            var start = await client.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
            Assert.Equal(HttpStatusCode.Created, start.StatusCode);
            var started = await start.Content.ReadFromJsonAsync<CandidateAttemptResponse>(JsonOptions);
            Assert.NotNull(started);
            var order = string.Join(',', started!.Questions.Select(q => q.Id));
            orders.Add(order);
            optionOrders.Add(string.Join(
                ',',
                OptionIds(started.Questions.Single(q => q.Id == scoredQuestion.Id).Body)));
            var startJson = await start.Content.ReadAsStringAsync();
            Assert.DoesNotContain("isCorrect", startJson, StringComparison.Ordinal);
            Assert.DoesNotContain("correctIndex", startJson, StringComparison.Ordinal);
            Assert.DoesNotContain("correctItemId", startJson, StringComparison.Ordinal);

            if (i == 0)
            {
                firstOrder = order;
                firstClient = client;
                firstAssignment = assignment;
                firstAttempt = started;
            }
        }

        Assert.True(orders.Distinct(StringComparer.Ordinal).Count() > 1);
        Assert.NotNull(firstClient);
        Assert.NotNull(firstAssignment);
        Assert.NotNull(firstAttempt);
        var current = await firstClient.GetFromJsonAsync<CandidateAttemptResponse>(
            $"/api/assignments/{firstAssignment.Id}/attempts/current",
            JsonOptions);
        Assert.Equal(firstOrder, string.Join(',', current!.Questions.Select(q => q.Id)));
        var startedMcq = firstAttempt.Questions.Single(q => q.Id == scoredQuestion.Id);
        var currentMcq = current.Questions.Single(q => q.Id == scoredQuestion.Id);
        Assert.Equal(OptionIds(startedMcq.Body), OptionIds(currentMcq.Body));
        Assert.Contains(optionOrders, order => order != "opt-a,opt-b,opt-right,opt-c");

        var save = await firstClient.PutAsJsonAsync(
            $"/api/assignments/{firstAssignment.Id}/attempts/current/answers",
            new SaveAnswersRequest
            {
                Answers =
                [
                    new AnswerDto
                    {
                        QuestionId = scoredQuestion.Id,
                        Value = JsonSerializer.SerializeToElement(new { optionId = "opt-right" }, JsonOptions)
                    }
                ]
            });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var submit = await firstClient.PostAsync(
            $"/api/assignments/{firstAssignment.Id}/attempts/current/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var detailJson = await recruiter.GetStringAsync($"/api/attempts/{firstAttempt.Id}");
        var detail = JsonSerializer.Deserialize<AttemptResultResponse>(detailJson, JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(canonicalIds, detail!.Items.Select(item => item.QuestionId).ToArray());
        var scored = detail.Items.Single(item => item.QuestionId == scoredQuestion.Id);
        Assert.Equal(1m, scored.PointsAwarded);
        Assert.Equal(["opt-a", "opt-b", "opt-right", "opt-c"], OptionIds(scored.Body!.Value));
        var ordering = detail.Items.Single(item => item.Stem == "Order these.");
        Assert.Equal(
            ["Third", "First", "Second"],
            ordering.Body!.Value.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("text").GetString()!).ToArray());
        Assert.DoesNotContain("correctIndex", detailJson, StringComparison.Ordinal);
    }

    private static string[] OptionIds(JsonElement body)
        => body.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("id").GetString()!).ToArray();

    private async Task<AssignmentResponse> AssignAsync(HttpClient recruiter, Guid quizId)
    {
        var post = await recruiter.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = $"shuffle.{Guid.NewGuid():N}@example.com",
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 }
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var assignment = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(assignment!.InviteUrl);
        return assignment;
    }

    private static async Task<Guid> CreateShuffledQuizAsync(HttpClient client)
    {
        var json = $$"""
            {
              "openingId": "{{DevelopmentOpeningSeeder.SampleOpeningBackend}}",
              "title": "Shuffled slice quiz",
              "expectedExperienceYears": 1,
              "shuffleQuestions": true,
              "shuffleOptions": true,
              "questions": [
                {
                  "type": "multipleChoiceSingle",
                  "stem": "Pick the right status.",
                  "scoringMode": "auto",
                  "points": 1,
                  "body": {
                    "options": [
                      { "id": "opt-a", "text": "200 OK", "isCorrect": false },
                      { "id": "opt-b", "text": "204 No Content", "isCorrect": false },
                      { "id": "opt-right", "text": "201 Created", "isCorrect": true },
                      { "id": "opt-c", "text": "404 Not Found", "isCorrect": false }
                    ]
                  }
                },
                {
                  "type": "multipleChoiceSingle",
                  "stem": "Second stem.",
                  "scoringMode": "auto",
                  "points": 1,
                  "body": {
                    "options": [
                      { "id": "opt-a", "text": "A", "isCorrect": true },
                      { "id": "opt-b", "text": "B", "isCorrect": false },
                      { "id": "opt-c", "text": "C", "isCorrect": false }
                    ]
                  }
                },
                {
                  "type": "ordering",
                  "stem": "Order these.",
                  "scoringMode": "auto",
                  "creditMode": "partial",
                  "points": 3,
                  "body": {
                    "items": [
                      { "id": "third", "text": "Third", "correctIndex": 2 },
                      { "id": "first", "text": "First", "correctIndex": 0 },
                      { "id": "second", "text": "Second", "correctIndex": 1 }
                    ]
                  }
                },
                {
                  "type": "trueFalse",
                  "stem": "A quiz belongs to one opening.",
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

    [RequiresDatabaseFact]
    public async Task Submit_omits_score_and_item_correctness_unless_show_score_is_on()
    {
        var (hiddenAssignment, hiddenQuestionId) = await CreateAutoAssignmentAsync();
        Assert.False(hiddenAssignment.ShowScoreToCandidate);
        var hiddenClient = await CandidateClientAsync(hiddenAssignment.InviteUrl!);
        Assert.Equal(
            HttpStatusCode.Created,
            (await hiddenClient.PostAsync($"/api/assignments/{hiddenAssignment.Id}/attempts", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await hiddenClient.PutAsJsonAsync(
                $"/api/assignments/{hiddenAssignment.Id}/attempts/current/answers",
                new SaveAnswersRequest
                {
                    Answers =
                    [
                        new AnswerDto
                        {
                            QuestionId = hiddenQuestionId,
                            Value = JsonSerializer.SerializeToElement(new { value = true }, JsonOptions)
                        }
                    ]
                })).StatusCode);
        var hiddenSubmit = await hiddenClient.PostAsync(
            $"/api/assignments/{hiddenAssignment.Id}/attempts/current/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, hiddenSubmit.StatusCode);
        var hiddenJson = await hiddenSubmit.Content.ReadAsStringAsync();
        Assert.Contains("\"resultStatus\":\"complete\"", hiddenJson, StringComparison.Ordinal);
        Assert.DoesNotContain("autoPointsAwarded", hiddenJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("autoPointsAvailable", hiddenJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("totalPointsAvailable", hiddenJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("itemResults", hiddenJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pointsAwarded", hiddenJson, StringComparison.OrdinalIgnoreCase);
        var hiddenCurrent = await hiddenClient.GetStringAsync(
            $"/api/assignments/{hiddenAssignment.Id}/attempts/current");
        Assert.DoesNotContain("itemResults", hiddenCurrent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pointsAwarded", hiddenCurrent, StringComparison.OrdinalIgnoreCase);

        var (shownAssignment, shownQuestionId) = await CreateAutoAssignmentAsync(showScoreToCandidate: true);
        Assert.True(shownAssignment.ShowScoreToCandidate);
        var shownClient = await CandidateClientAsync(shownAssignment.InviteUrl!);
        Assert.Equal(
            HttpStatusCode.Created,
            (await shownClient.PostAsync($"/api/assignments/{shownAssignment.Id}/attempts", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await shownClient.PutAsJsonAsync(
                $"/api/assignments/{shownAssignment.Id}/attempts/current/answers",
                new SaveAnswersRequest
                {
                    Answers =
                    [
                        new AnswerDto
                        {
                            QuestionId = shownQuestionId,
                            Value = JsonSerializer.SerializeToElement(new { value = true }, JsonOptions)
                        }
                    ]
                })).StatusCode);
        var shownSubmit = await shownClient.PostAsync(
            $"/api/assignments/{shownAssignment.Id}/attempts/current/submit",
            null);
        var shown = await shownSubmit.Content.ReadFromJsonAsync<CandidateSubmitResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, shownSubmit.StatusCode);
        Assert.Equal(1m, shown!.AutoPointsAwarded);
        Assert.NotNull(shown.ItemResults);
        Assert.Equal("scored", shown.ItemResults![0].Status);
        Assert.Equal(1m, shown.ItemResults[0].PointsAwarded);
    }

    private async Task<(AssignmentResponse Assignment, Guid QuestionId)> CreateAutoAssignmentAsync(
        bool showScoreToCandidate = false)
    {
        var recruiter = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Evaluation.AttemptsRead));
        var quizId = await CreateAutoQuizAsync(recruiter);
        var quiz = await recruiter.GetFromJsonAsync<QuizResponse>($"/api/quizzes/{quizId}", CatalogJson.SerializerOptions);
        var questionId = quiz!.Questions[0].Id;

        var post = await recruiter.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = $"auto.{Guid.NewGuid():N}@example.com",
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 },
            ShowScoreToCandidate = showScoreToCandidate
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var assignment = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(assignment);
        Assert.Equal(showScoreToCandidate, assignment!.ShowScoreToCandidate);
        return (assignment, questionId);
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
        var created = await response.Content.ReadFromJsonAsync<QuizResponse>(CatalogJson.SerializerOptions);
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
