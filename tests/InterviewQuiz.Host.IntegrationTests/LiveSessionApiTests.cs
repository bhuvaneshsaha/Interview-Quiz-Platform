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
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class LiveSessionApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = CatalogJson.SerializerOptions;

    public LiveSessionApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Unfreeze();
    }

    [RequiresDatabaseFact]
    public async Task Host_start_creates_attempt_and_candidate_start_is_rejected()
    {
        var email = $"live.{Guid.NewGuid():N}@example.com";
        var (assignment, host) = await CreateLiveAsync(email, 30);
        var invite = await host.PostAsync($"/api/assignments/{assignment.Id}/invite", null);
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var issued = await invite.Content.ReadFromJsonAsync<InviteResponse>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(issued!.InviteUrl));

        var candidate = await CandidateClientAsync(issued.InviteUrl);
        var preview = await candidate.GetFromJsonAsync<CandidateAttemptPreviewResponse>(
            $"/api/assignments/{assignment.Id}/attempts/preview",
            JsonOptions);
        Assert.Equal("waiting", preview!.LiveState);

        var selfStart = await candidate.PostAsync($"/api/assignments/{assignment.Id}/attempts", null);
        Assert.Equal(HttpStatusCode.BadRequest, selfStart.StatusCode);
        Assert.Contains(LiveSessionMessages.WaitingForHost, await selfStart.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var start = await host.PostAsync($"/api/assignments/{assignment.Id}/live/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var progress = await start.Content.ReadFromJsonAsync<LiveProgressResponse>(JsonOptions);
        Assert.Equal("running", progress!.State);
        Assert.NotNull(progress.AttemptId);

        var current = await candidate.GetFromJsonAsync<CandidateAttemptResponse>(
            $"/api/assignments/{assignment.Id}/attempts/current",
            JsonOptions);
        Assert.Equal(current!.StartedAtUtc.AddMinutes(30), current.DueAtUtc);

        var events = await EventsAsync(assignment.Id);
        Assert.Contains(events, row => row.Action == "linkIssued" && row.ActorUserId == "integration-user");
        Assert.Contains(events, row => row.Action == "candidateJoined" && row.ActorUserId is null);
        Assert.Contains(events, row => row.Action == "started" && row.ActorUserId == "integration-user");

        var raw = await (await host.GetAsync($"/api/assignments/{assignment.Id}/live/progress")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(email, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"value\"", raw, StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Pause_freezes_the_due_clock_and_blocks_answers()
    {
        var (assignment, host) = await CreateLiveAsync($"pause.{Guid.NewGuid():N}@example.com", 30);
        var candidate = await JoinAsync(assignment.Id, host);
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/assignments/{assignment.Id}/live/start", null)).StatusCode);
        var before = await candidate.GetFromJsonAsync<CandidateAttemptResponse>(
            $"/api/assignments/{assignment.Id}/attempts/current",
            JsonOptions);
        var questionId = before!.Questions[0].Id;
        var save = await SaveAsync(candidate, assignment.Id, questionId, true);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        _factory.Clock.Freeze(DateTimeOffset.UtcNow);
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/assignments/{assignment.Id}/live/pause", null)).StatusCode);
            _factory.Clock.Advance(TimeSpan.FromSeconds(120));
            var resume = await host.PostAsync($"/api/assignments/{assignment.Id}/live/resume", null);
            Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
            var progress = await resume.Content.ReadFromJsonAsync<LiveProgressResponse>(JsonOptions);
            Assert.Equal(120, progress!.TotalPausedSeconds);

            var after = await candidate.GetFromJsonAsync<CandidateAttemptResponse>(
                $"/api/assignments/{assignment.Id}/attempts/current",
                JsonOptions);
            var moved = (after!.DueAtUtc - before.DueAtUtc).TotalSeconds;
            Assert.InRange(moved, 119, 121);
        }
        finally
        {
            _factory.Clock.Unfreeze();
        }

        var (pausedAssignment, pausedHost) = await CreateLiveAsync($"paused-save.{Guid.NewGuid():N}@example.com", 30);
        var pausedCandidate = await JoinAsync(pausedAssignment.Id, pausedHost);
        Assert.Equal(HttpStatusCode.OK, (await pausedHost.PostAsync($"/api/assignments/{pausedAssignment.Id}/live/start", null)).StatusCode);
        var running = await pausedCandidate.GetFromJsonAsync<CandidateAttemptResponse>(
            $"/api/assignments/{pausedAssignment.Id}/attempts/current",
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(pausedCandidate, pausedAssignment.Id, running!.Questions[0].Id, true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await pausedHost.PostAsync($"/api/assignments/{pausedAssignment.Id}/live/pause", null)).StatusCode);
        var rejected = await SaveAsync(pausedCandidate, pausedAssignment.Id, running.Questions[0].Id, false);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("Session is paused.", await rejected.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var unchanged = await pausedCandidate.GetFromJsonAsync<CandidateAttemptResponse>(
            $"/api/assignments/{pausedAssignment.Id}/attempts/current",
            JsonOptions);
        Assert.True(unchanged!.Answers[0].Value.GetProperty("value").GetBoolean());
    }

    [RequiresDatabaseFact]
    public async Task Pause_does_not_expire_and_a_running_session_times_out()
    {
        var (held, host) = await CreateLiveAsync($"held.{Guid.NewGuid():N}@example.com", 1);
        var candidate = await JoinAsync(held.Id, host);
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/assignments/{held.Id}/live/start", null)).StatusCode);
        _factory.Clock.Freeze(DateTimeOffset.UtcNow);
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/assignments/{held.Id}/live/pause", null)).StatusCode);
            _factory.Clock.Advance(TimeSpan.FromSeconds(90));
            var current = await candidate.GetFromJsonAsync<CandidateAttemptResponse>(
                $"/api/assignments/{held.Id}/attempts/current",
                JsonOptions);
            Assert.Equal("inProgress", current!.Status);
            Assert.True(current.Paused);
        }
        finally
        {
            _factory.Clock.Unfreeze();
        }

        var (running, runningHost) = await CreateLiveAsync($"timeout.{Guid.NewGuid():N}@example.com", 1);
        var runningCandidate = await JoinAsync(running.Id, runningHost);
        Assert.Equal(HttpStatusCode.OK, (await runningHost.PostAsync($"/api/assignments/{running.Id}/live/start", null)).StatusCode);
        _factory.Clock.Freeze(DateTimeOffset.UtcNow);
        try
        {
            _factory.Clock.Advance(TimeSpan.FromSeconds(61));
            var progress = await runningHost.GetFromJsonAsync<LiveProgressResponse>(
                $"/api/assignments/{running.Id}/live/progress",
                JsonOptions);
            Assert.Equal("timeout", progress!.EndReason);
            Assert.Equal("submitted", progress.State);
            var current = await runningCandidate.GetFromJsonAsync<CandidateAttemptResponse>(
                $"/api/assignments/{running.Id}/attempts/current",
                JsonOptions);
            Assert.Equal("submitted", current!.Status);
            Assert.Equal("timeout", current.LiveEndReason);
        }
        finally
        {
            _factory.Clock.Unfreeze();
        }
    }

    [RequiresDatabaseFact]
    public async Task Pause_longer_than_fifteen_minutes_auto_ends()
    {
        var (assignment, host) = await CreateLiveAsync($"cap.{Guid.NewGuid():N}@example.com", 30);
        var candidate = await JoinAsync(assignment.Id, host);
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/assignments/{assignment.Id}/live/start", null)).StatusCode);
        _factory.Clock.Freeze(DateTimeOffset.UtcNow);
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/assignments/{assignment.Id}/live/pause", null)).StatusCode);
            _factory.Clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
            var progress = await host.GetFromJsonAsync<LiveProgressResponse>(
                $"/api/assignments/{assignment.Id}/live/progress",
                JsonOptions);
            Assert.Equal("pauseLimit", progress!.EndReason);
            Assert.Equal("submitted", progress.State);
            var current = await candidate.GetFromJsonAsync<CandidateAttemptResponse>(
                $"/api/assignments/{assignment.Id}/attempts/current",
                JsonOptions);
            Assert.Equal("submitted", current!.Status);
            var events = await EventsAsync(assignment.Id);
            Assert.Contains(events, row => row.Action == "autoEnded");
        }
        finally
        {
            _factory.Clock.Unfreeze();
        }
    }

    [RequiresDatabaseFact]
    public async Task Host_end_is_idempotent()
    {
        var (assignment, host) = await CreateLiveAsync($"end.{Guid.NewGuid():N}@example.com", 30);
        await JoinAsync(assignment.Id, host);
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/assignments/{assignment.Id}/live/start", null)).StatusCode);
        var first = await host.PostAsync($"/api/assignments/{assignment.Id}/live/end", null);
        var second = await host.PostAsync($"/api/assignments/{assignment.Id}/live/end", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var progress = await second.Content.ReadFromJsonAsync<LiveProgressResponse>(JsonOptions);
        Assert.Equal("host", progress!.EndReason);
        Assert.Equal("submitted", progress.State);

        var detail = await host.GetFromJsonAsync<AssignmentResponse>($"/api/assignments/{assignment.Id}", JsonOptions);
        Assert.Contains(detail!.Status, new[] { "completed", "pendingReview" });
        var ended = (await EventsAsync(assignment.Id)).Count(row => row.Action == "ended");
        Assert.Equal(1, ended);
    }

    [RequiresDatabaseFact]
    public async Task Permissions_reject_the_wrong_caller()
    {
        var (assignment, host) = await CreateLiveAsync($"perm.{Guid.NewGuid():N}@example.com", 30);
        var candidate = await JoinAsync(assignment.Id, host);

        var reader = Client(JwtTestTokens.Create(PermissionCodes.Evaluation.AttemptsRead));
        var progress = await reader.GetAsync($"/api/assignments/{assignment.Id}/live/progress");
        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
        var denied = await reader.PostAsync($"/api/assignments/{assignment.Id}/live/start", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var noLive = Client(JwtTestTokens.Create(PermissionCodes.Delivery.AssignmentsRead));
        Assert.Equal(HttpStatusCode.Forbidden, (await noLive.PostAsync($"/api/assignments/{assignment.Id}/live/pause", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noLive.PostAsync($"/api/assignments/{assignment.Id}/live/resume", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noLive.PostAsync($"/api/assignments/{assignment.Id}/live/end", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.PostAsync($"/api/assignments/{assignment.Id}/live/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync($"/api/assignments/{assignment.Id}/live/progress")).StatusCode);

        var asyncAssignment = await CreateAsyncAssignmentAsync();
        var asyncStart = await host.PostAsync($"/api/assignments/{asyncAssignment.Id}/live/start", null);
        Assert.Equal(HttpStatusCode.BadRequest, asyncStart.StatusCode);
        Assert.Contains(LiveSessionMessages.NotLive, await asyncStart.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Missing_duration_cannot_start_and_cannot_be_created()
    {
        var client = Client(HostPermissions());
        var quizId = await CreateQuizAsync(client);
        var missing = await client.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = $"noduration.{Guid.NewGuid():N}@example.com",
            Mode = "live"
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains(Assignment.LiveDurationRequiredMessage, await missing.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var (assignment, host) = await CreateLiveAsync($"legacy.{Guid.NewGuid():N}@example.com", 30);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
            var row = await db.Assignments.SingleAsync(item => item.Id == assignment.Id);
            typeof(Assignment).GetProperty(nameof(Assignment.OverallDurationMinutes))!
                .SetValue(row, null);
            await db.SaveChangesAsync();
        }

        await JoinAsync(assignment.Id, host);
        var start = await host.PostAsync($"/api/assignments/{assignment.Id}/live/start", null);
        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
        Assert.Contains(LiveSessionMessages.DurationRequiredToStart, await start.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Start_requires_a_recent_lobby_join_and_reports_flags()
    {
        var (assignment, host) = await CreateLiveAsync($"lobby.{Guid.NewGuid():N}@example.com", 30);
        var candidate = await JoinAsync(assignment.Id, host);
        var connected = await host.GetFromJsonAsync<LiveProgressResponse>(
            $"/api/assignments/{assignment.Id}/live/progress",
            JsonOptions);
        Assert.True(connected!.CandidateConnected);

        _factory.Clock.Freeze(DateTimeOffset.UtcNow);
        try
        {
            _factory.Clock.Advance(TimeSpan.FromSeconds(16));
            var stale = await host.GetFromJsonAsync<LiveProgressResponse>(
                $"/api/assignments/{assignment.Id}/live/progress",
                JsonOptions);
            Assert.False(stale!.CandidateConnected);

            _factory.Clock.Advance(TimeSpan.FromSeconds(16));
            var blocked = await host.PostAsync($"/api/assignments/{assignment.Id}/live/start", null);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Contains(LiveSessionMessages.CandidateNotJoined, await blocked.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync($"/api/assignments/{assignment.Id}/attempts/preview")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/assignments/{assignment.Id}/live/start", null)).StatusCode);
        }
        finally
        {
            _factory.Clock.Unfreeze();
        }

        var current = await candidate.GetFromJsonAsync<CandidateAttemptResponse>(
            $"/api/assignments/{assignment.Id}/attempts/current",
            JsonOptions);
        var questionId = current!.Questions[0].Id;
        var flags = await candidate.PutAsJsonAsync(
            $"/api/assignments/{assignment.Id}/attempts/current/flags",
            new SetReviewFlagsRequest
            {
                Flags = [new ReviewFlagDto { QuestionId = questionId, Flagged = true }]
            });
        Assert.Equal(HttpStatusCode.OK, flags.StatusCode);
        var position = await candidate.PutAsJsonAsync(
            $"/api/assignments/{assignment.Id}/attempts/current/position",
            new ReportPositionRequest { QuestionIndex = 0 });
        Assert.Equal(HttpStatusCode.NoContent, position.StatusCode);
        var progress = await host.GetFromJsonAsync<LiveProgressResponse>(
            $"/api/assignments/{assignment.Id}/live/progress",
            JsonOptions);
        Assert.Equal(1, progress!.FlaggedCount);
        Assert.Equal(0, progress.CurrentQuestionIndex);
    }

    private async Task<(AssignmentResponse Assignment, HttpClient Host)> CreateLiveAsync(string email, int minutes)
    {
        var host = Client(HostPermissions());
        var quizId = await CreateQuizAsync(host);
        var post = await host.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = email,
            Mode = "live",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = minutes }
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var assignment = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(assignment);
        Assert.Null(assignment!.InviteUrl);
        return (assignment, host);
    }

    private async Task<AssignmentResponse> CreateAsyncAssignmentAsync()
    {
        var (assignment, _) = await CreateNamedAsync("async");
        return assignment;
    }

    private async Task<(AssignmentResponse Assignment, HttpClient Host)> CreateNamedAsync(string mode)
    {
        var host = Client(HostPermissions());
        var quizId = await CreateQuizAsync(host);
        var post = await host.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = quizId,
            CandidateEmail = $"{mode}.{Guid.NewGuid():N}@example.com",
            Mode = mode,
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 }
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var assignment = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        return (assignment!, host);
    }

    private async Task<HttpClient> JoinAsync(Guid assignmentId, HttpClient host)
    {
        var invite = await host.PostAsync($"/api/assignments/{assignmentId}/invite", null);
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var issued = await invite.Content.ReadFromJsonAsync<InviteResponse>(JsonOptions);
        return await CandidateClientAsync(issued!.InviteUrl);
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

    private async Task<List<LiveSessionEvent>> EventsAsync(Guid assignmentId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        return await db.LiveSessionEvents.Where(row => row.AssignmentId == assignmentId).ToListAsync();
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

    private static async Task<Guid> CreateQuizAsync(HttpClient client)
    {
        var json = $$"""
            {
              "openingId": "{{DevelopmentOpeningSeeder.SampleOpeningBackend}}",
              "title": "Live session quiz",
              "expectedExperienceYears": 1,
              "questions": [
                {
                  "type": "trueFalse",
                  "stem": "A live session has one candidate.",
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

    private HttpClient Client(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static string HostPermissions()
        => JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Delivery.SessionsLiveRun,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Evaluation.AttemptsRead);
}
