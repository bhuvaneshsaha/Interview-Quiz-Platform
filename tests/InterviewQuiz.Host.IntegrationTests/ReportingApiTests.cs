using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Delivery.Infrastructure.Persistence;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Host.Composition;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using InterviewQuiz.Search.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class ReportingApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = CatalogJson.SerializerOptions;

    public ReportingApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Unfreeze();
    }

    [Fact]
    public void Csv_cap_rejects_more_than_10000_rows()
    {
        var error = Assert.Throws<InterviewQuiz.Kernel.Exceptions.DomainException>(
            () => ReportCsv.EnsureWithinCap(ReportCsv.MaxRows + 1));
        Assert.Equal(ReportCsv.OverCapMessage, error.Message);
        ReportCsv.EnsureWithinCap(ReportCsv.MaxRows);
    }

    [RequiresDatabaseFact]
    public async Task Reports_require_reports_read()
    {
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/reports/funnel")).StatusCode);

        var attemptsOnly = Client(JwtTestTokens.Create(PermissionCodes.Evaluation.AttemptsRead));
        Assert.Equal(HttpStatusCode.Forbidden, (await attemptsOnly.GetAsync("/api/reports/funnel")).StatusCode);

        var reviewer = Client(JwtTestTokens.Create(
            PermissionCodes.Openings.Read,
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Evaluation.AttemptsRead,
            PermissionCodes.Evaluation.AttemptsReview));
        var denied = await reviewer.GetAsync("/api/reports/scores");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = Client(JwtTestTokens.Create(PermissionCodes.Evaluation.ReportsRead));
        Assert.Equal(HttpStatusCode.OK, (await allowed.GetAsync("/api/reports/funnel")).StatusCode);
        var missing = await allowed.GetAsync("/api/reports/not-a-report/export");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Mixed_quiz_matches_opening_summary_and_keeps_unsettled_points_out_of_pass()
    {
        var frozen = DateTimeOffset.UtcNow.AddSeconds(-90);
        _factory.Clock.Freeze(frozen);
        try
        {
            var client = Client(Reporter());
            var (quizId, autoQuestionId) = await CreateMixedQuizAsync(client);
            var emailA = $"pass.{Guid.NewGuid():N}@example.com";
            var emailB = $"fail.{Guid.NewGuid():N}@example.com";
            var emailC = $"idle.{Guid.NewGuid():N}@example.com";
            var assignmentA = await CreateAsyncAssignmentAsync(client, quizId, emailA);
            var assignmentB = await CreateAsyncAssignmentAsync(client, quizId, emailB);
            var assignmentC = await CreateAsyncAssignmentAsync(client, quizId, emailC);

            var candidateA = await CandidateClientAsync(assignmentA.InviteUrl!);
            Assert.Equal(HttpStatusCode.Created, (await candidateA.PostAsync($"/api/assignments/{assignmentA.Id}/attempts", null)).StatusCode);
            _factory.Clock.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.OK, (await SaveAsync(candidateA, assignmentA.Id, autoQuestionId, true)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await candidateA.PostAsync($"/api/assignments/{assignmentA.Id}/attempts/current/submit", null)).StatusCode);

            _factory.Clock.Advance(TimeSpan.FromSeconds(20));
            var candidateB = await CandidateClientAsync(assignmentB.InviteUrl!);
            Assert.Equal(HttpStatusCode.Created, (await candidateB.PostAsync($"/api/assignments/{assignmentB.Id}/attempts", null)).StatusCode);
            _factory.Clock.Advance(TimeSpan.FromSeconds(20));
            Assert.Equal(HttpStatusCode.OK, (await SaveAsync(candidateB, assignmentB.Id, autoQuestionId, false)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await candidateB.PostAsync($"/api/assignments/{assignmentB.Id}/attempts/current/submit", null)).StatusCode);

            var quizCriteria = Criteria(("quizId", "is", quizId.ToString()));
            var summary = await client.GetFromJsonAsync<OpeningSummaryResponse>(
                $"/api/openings/{DevelopmentOpeningSeeder.SampleOpeningBackend}/summary",
                JsonOptions);
            var quizSummary = summary!.Quizzes!.Single(quiz => quiz.Id == quizId);
            var funnel = await GetReportAsync<FunnelReportResponse>(client, "funnel", quizCriteria);
            var row = funnel.Items.Single(item => item.QuizId == quizId);
            Assert.Equal(3, quizSummary.Candidates!.Assigned);
            Assert.Equal(2, quizSummary.Candidates.Appeared);
            Assert.Equal(2, quizSummary.Candidates.Submitted);
            Assert.Equal(quizSummary.Candidates.Assigned, row.Assigned);
            Assert.Equal(quizSummary.Candidates.Appeared, row.Appeared);
            Assert.Equal(quizSummary.Candidates.Submitted, row.Submitted);
            Assert.Equal(0, row.Completed);
            Assert.Equal(2, row.PendingReview);
            Assert.Equal(0.6667m, row.AppearedRate);
            Assert.Equal(0m, row.CompletedRate);
            Assert.Equal(row.Assigned, funnel.Totals.Assigned);
            Assert.True(funnel.DefaultWindowApplied);

            var scores = await GetReportAsync<ScoresReportResponse>(client, "scores", quizCriteria);
            Assert.Equal(0.70m, scores.PassThreshold);
            Assert.Equal(2, scores.PassDenominator);
            Assert.Equal(1, scores.PassCount);
            Assert.Equal(0.5m, scores.PassRate);
            var passed = scores.Items.Single(item => item.CandidateEmail == emailA);
            var failed = scores.Items.Single(item => item.CandidateEmail == emailB);
            Assert.True(passed.Passed);
            Assert.False(failed.Passed);
            Assert.Equal(10m, passed.AutoPointsAwarded);
            Assert.Equal(10m, passed.AutoPointsAvailable);
            Assert.Equal(20m, passed.TotalPointsAvailable);
            Assert.Equal(1, passed.UnsettledCount);
            Assert.Equal("incomplete", passed.ResultStatus);
            Assert.Equal("pendingReview", passed.Status);

            var questions = await GetReportAsync<QuestionsReportResponse>(client, "questions", quizCriteria);
            var question = questions.Items.Single();
            Assert.Equal(autoQuestionId, question.QuestionId);
            Assert.Equal(2, question.SettledCount);
            Assert.Equal(0.5m, question.Difficulty);
            Assert.Null(question.Discrimination);
            Assert.True(question.DiscriminationHidden);
            Assert.DoesNotContain("correct", question.Stem, StringComparison.OrdinalIgnoreCase);

            var candidates = await GetReportAsync<CandidatesReportResponse>(client, "candidates", quizCriteria);
            Assert.Equal(emailA, candidates.Items[0].CandidateEmail);
            Assert.Equal(1, candidates.Items[0].Rank);
            Assert.Equal(emailB, candidates.Items[1].CandidateEmail);
            Assert.Equal(emailC, candidates.Items[2].CandidateEmail);
            Assert.Equal("notStarted", candidates.Items[2].Status);
            Assert.Null(candidates.Items[2].AutoPointsAwarded);

            var pending = await GetReportAsync<PendingReviewReportResponse>(client, "pending-review", quizCriteria);
            Assert.Equal(2, pending.TotalCount);
            Assert.Equal(emailA, pending.Items[0].CandidateEmail);
            Assert.True(pending.Items[0].AgeHours > pending.Items[1].AgeHours);
            Assert.Equal(1, pending.Items[0].UnsettledCount);

            var export = await client.GetAsync($"/api/reports/candidates/export?criteria={Uri.EscapeDataString(quizCriteria)}");
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
            Assert.Equal("text/csv; charset=utf-8", export.Content.Headers.ContentType?.ToString());
            Assert.Contains("candidates.csv", export.Content.Headers.ContentDisposition?.FileName);
            var bytes = await export.Content.ReadAsByteArrayAsync();
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
            var csv = Encoding.UTF8.GetString(bytes[3..]);
            Assert.StartsWith(
                "rank,openingId,openingTitle,quizId,quizTitle,candidateEmail,status,autoPointsAwarded,autoPointsAvailable,durationSeconds,mode,snapshotId,quizRowVersion,assignmentId,attemptId\n",
                csv,
                StringComparison.Ordinal);
            Assert.Contains(emailA, csv, StringComparison.Ordinal);
            Assert.Contains(emailB, csv, StringComparison.Ordinal);
            Assert.Contains(emailC, csv, StringComparison.Ordinal);
            Assert.DoesNotContain("isCorrect", csv, StringComparison.Ordinal);
            Assert.DoesNotContain("acceptableAnswers", csv, StringComparison.Ordinal);
            Assert.DoesNotContain("correctItemId", csv, StringComparison.Ordinal);

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
                var idle = await db.Assignments.SingleAsync(item => item.Id == assignmentC.Id);
                db.Entry(idle).Property(nameof(Assignment.CreatedAtUtc)).CurrentValue = frozen.AddDays(-100);
                await db.SaveChangesAsync();
            }

            var windowed = await GetReportAsync<FunnelReportResponse>(client, "funnel", quizCriteria);
            Assert.Equal(2, windowed.Items.Single().Assigned);
            Assert.True(windowed.DefaultWindowApplied);

            var widened = Criteria(
                ("quizId", "is", quizId.ToString()),
                ("createdAtUtc", ">", frozen.AddDays(-120)));
            var included = await GetReportAsync<FunnelReportResponse>(client, "funnel", widened);
            Assert.Equal(3, included.Items.Single().Assigned);
            Assert.False(included.DefaultWindowApplied);

            var unknown = await client.GetAsync(
                "/api/reports/funnel?criteria=" + Uri.EscapeDataString(
                    """{"filters":[{"field":"originTemplateId","operator":"is","value":"x"}]}"""));
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
            Assert.Contains("Unknown filter field", await unknown.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var mismatch = await client.GetAsync(
                "/api/reports/funnel?criteria=" + Uri.EscapeDataString(Criteria(
                    ("quizId", "is", quizId.ToString()),
                    ("openingId", "is", Guid.NewGuid().ToString()))));
            Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
            Assert.Contains(ReportingComposer.QuizOpeningMismatchMessage, await mismatch.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var filterName = "Funnel " + Guid.NewGuid().ToString("N")[..8];
            var save = await client.PostAsync(
                "/api/filters",
                new StringContent(
                    $$"""
                    {
                      "name": "{{filterName}}",
                      "target": "reportsFunnel",
                      "criteria": { "filters": [ { "field": "quizId", "operator": "is", "value": "{{quizId}}" } ] }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"));
            Assert.Equal(HttpStatusCode.Created, save.StatusCode);
            var listed = await client.GetFromJsonAsync<PagedResult<FilterResponse>>(
                "/api/filters?target=reportsFunnel&pageSize=100",
                JsonOptions);
            Assert.Contains(listed!.Items, item => item.Name == filterName && item.Target == InterviewQuiz.Search.Domain.FilterTarget.ReportsFunnel);
        }
        finally
        {
            _factory.Clock.Unfreeze();
        }
    }

    [RequiresDatabaseFact]
    public async Task Live_time_subtracts_pause_once_and_async_ignores_it()
    {
        _factory.Clock.Freeze(DateTimeOffset.UtcNow.AddSeconds(-200));
        try
        {
            var client = Client(Reporter());
            var liveQuiz = await CreateAutoQuizAsync(client, "Live report quiz");
            var live = await CreateAssignmentAsync(client, liveQuiz, $"live.{Guid.NewGuid():N}@example.com", "live");
            var candidate = await JoinAsync(client, live.Id);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/assignments/{live.Id}/live/start", null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/assignments/{live.Id}/live/pause", null)).StatusCode);
            _factory.Clock.Advance(TimeSpan.FromSeconds(120));
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/assignments/{live.Id}/live/resume", null)).StatusCode);
            _factory.Clock.Advance(TimeSpan.FromSeconds(30));
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsync($"/api/assignments/{live.Id}/attempts/current/submit", null)).StatusCode);

            var liveReport = await GetReportAsync<TimeReportResponse>(
                client,
                "time",
                Criteria(("quizId", "is", liveQuiz.ToString())));
            Assert.InRange(liveReport.Items.Single().DurationSeconds, 29, 31);

            var asyncQuiz = await CreateAutoQuizAsync(client, "Async report quiz");
            var asyncAssignment = await CreateAssignmentAsync(client, asyncQuiz, $"async.{Guid.NewGuid():N}@example.com", "async");
            var asyncCandidate = await CandidateClientAsync(asyncAssignment.InviteUrl!);
            Assert.Equal(HttpStatusCode.Created, (await asyncCandidate.PostAsync($"/api/assignments/{asyncAssignment.Id}/attempts", null)).StatusCode);
            _factory.Clock.Advance(TimeSpan.FromSeconds(45));
            Assert.Equal(HttpStatusCode.OK, (await asyncCandidate.PostAsync($"/api/assignments/{asyncAssignment.Id}/attempts/current/submit", null)).StatusCode);
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
                var row = await db.Assignments.SingleAsync(item => item.Id == asyncAssignment.Id);
                db.Entry(row).Property(nameof(Assignment.TotalPausedSeconds)).CurrentValue = 120;
                await db.SaveChangesAsync();
            }

            var asyncReport = await GetReportAsync<TimeReportResponse>(
                client,
                "time",
                Criteria(("quizId", "is", asyncQuiz.ToString())));
            Assert.InRange(asyncReport.Items.Single().DurationSeconds, 44, 46);
        }
        finally
        {
            _factory.Clock.Unfreeze();
        }
    }

    private static async Task<T> GetReportAsync<T>(HttpClient client, string route, string criteria)
    {
        var response = await client.GetAsync($"/api/reports/{route}?criteria={Uri.EscapeDataString(criteria)}&pageSize=100");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }

    private static string Criteria(params (string Field, string Operator, object Value)[] chips)
    {
        var payload = new
        {
            filters = chips.Select(chip => new { field = chip.Field, @operator = chip.Operator, value = chip.Value })
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private async Task<(Guid QuizId, Guid AutoQuestionId)> CreateMixedQuizAsync(HttpClient client)
    {
        var json = $$"""
            {
              "openingId": "{{DevelopmentOpeningSeeder.SampleOpeningBackend}}",
              "title": "Reporting mixed quiz",
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
                }
              ]
            }
            """;
        var response = await client.PostAsync("/api/quizzes", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        var quiz = await client.GetFromJsonAsync<QuizResponse>($"/api/quizzes/{created!.Id}", JsonOptions);
        var auto = quiz!.Questions.Single(question => question.Type == QuestionType.TrueFalse);
        return (quiz.Id, auto.Id);
    }

    private static async Task<Guid> CreateAutoQuizAsync(HttpClient client, string title)
    {
        var json = $$"""
            {
              "openingId": "{{DevelopmentOpeningSeeder.SampleOpeningBackend}}",
              "title": "{{title}}",
              "expectedExperienceYears": 1,
              "questions": [
                {
                  "type": "trueFalse",
                  "stem": "Time is measured on the server.",
                  "scoringMode": "auto",
                  "points": 1,
                  "body": { "correct": true }
                }
              ]
            }
            """;
        var response = await client.PostAsync("/api/quizzes", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        return created!.Id;
    }

    private static Task<AssignmentResponse> CreateAsyncAssignmentAsync(HttpClient client, Guid quizId, string email)
        => CreateAssignmentAsync(client, quizId, email, "async");

    private static async Task<AssignmentResponse> CreateAssignmentAsync(HttpClient client, Guid quizId, string email, string mode)
    {
        var response = await client.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = email,
            Mode = mode,
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var assignment = await response.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        return assignment!;
    }

    private async Task<HttpClient> JoinAsync(HttpClient host, Guid assignmentId)
    {
        var invite = await host.PostAsync($"/api/assignments/{assignmentId}/invite", null);
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var issued = await invite.Content.ReadFromJsonAsync<InviteResponse>(JsonOptions);
        var candidate = await CandidateClientAsync(issued!.InviteUrl);
        Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync($"/api/assignments/{assignmentId}/attempts/preview")).StatusCode);
        return candidate;
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

    private static string Reporter()
        => JwtTestTokens.Create(
            PermissionCodes.Evaluation.ReportsRead,
            PermissionCodes.Evaluation.AttemptsRead,
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Delivery.SessionsLiveRun,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Openings.Read,
            PermissionCodes.Search.FiltersWrite);
}
