using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class AssignmentsApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public AssignmentsApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Recruiter_with_assignments_write_creates_async_assignment_with_inviteUrl()
    {
        var client = CreateAuthenticatedClient(Recruiter());
        var post = await client.PostAsJsonAsync("/api/assignments", ValidCreate());
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.NotNull(post.Headers.Location);

        var created = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created!.InviteUrl));
        Assert.StartsWith("http://localhost:4200/attempt?token=", created.InviteUrl, StringComparison.Ordinal);
        Assert.Equal("async", created.Mode);
        Assert.Equal(30, created.OverallDurationMinutes);

        var get = await client.GetAsync($"/api/assignments/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var loaded = await get.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(loaded);
        Assert.Null(loaded!.InviteUrl);
        Assert.Equal(created.Id, loaded.Id);
    }

    [RequiresDatabaseFact]
    public async Task Author_without_assignments_write_is_forbidden()
    {
        var client = CreateAuthenticatedClient(JwtTestTokens.Create(
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite));
        var response = await client.PostAsJsonAsync("/api/assignments", ValidCreate());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Create_unknown_opening_is_bad_request()
    {
        var client = CreateAuthenticatedClient(Recruiter());
        var request = ValidCreate();
        request.OpeningId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var response = await client.PostAsJsonAsync("/api/assignments", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Opening does not exist.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Create_unknown_quiz_is_bad_request()
    {
        var client = CreateAuthenticatedClient(Recruiter());
        var request = ValidCreate();
        request.QuizId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var response = await client.PostAsJsonAsync("/api/assignments", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Quiz does not exist.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Create_quiz_wrong_opening_is_bad_request()
    {
        var client = CreateAuthenticatedClient(Recruiter());
        var request = ValidCreate();
        request.OpeningId = DevelopmentOpeningSeeder.SampleOpeningQa;
        var response = await client.PostAsJsonAsync("/api/assignments", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "Quiz does not belong to that opening.",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Candidate_jwt_cannot_post_assignments()
    {
        var recruiter = CreateAuthenticatedClient(Recruiter());
        var created = await (await recruiter.PostAsJsonAsync("/api/assignments", ValidCreate()))
            .Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        var token = MagicLinkApiTests.TokenFromInviteUrl(created!.InviteUrl!);

        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new InterviewQuiz.Access.Application.Contracts.ConsumeMagicLinkRequest { Token = token });
        var candidate = await consume.Content.ReadFromJsonAsync<InterviewQuiz.Access.Application.Contracts.CandidateTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", candidate!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/assignments", ValidCreate());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Live_assignment_omits_inviteUrl()
    {
        var client = CreateAuthenticatedClient(Recruiter());
        var request = ValidCreate();
        request.Mode = "live";
        request.Timing = null;
        var post = await client.PostAsJsonAsync("/api/assignments", request);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Null(created!.InviteUrl);
        Assert.Equal("live", created.Mode);
    }

    [RequiresDatabaseFact]
    public async Task Invite_rotate_invalidates_old_token()
    {
        var client = CreateAuthenticatedClient(Recruiter());
        var created = await (await client.PostAsJsonAsync("/api/assignments", ValidCreate()))
            .Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        var first = MagicLinkApiTests.TokenFromInviteUrl(created!.InviteUrl!);

        var invite = await client.PostAsync($"/api/assignments/{created.Id}/invite", null);
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var rotated = await invite.Content.ReadFromJsonAsync<InviteResponse>(JsonOptions);
        var second = MagicLinkApiTests.TokenFromInviteUrl(rotated!.InviteUrl);

        var anonymous = _factory.CreateClient();
        var old = await anonymous.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new InterviewQuiz.Access.Application.Contracts.ConsumeMagicLinkRequest { Token = first });
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);

        var current = await anonymous.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new InterviewQuiz.Access.Application.Contracts.ConsumeMagicLinkRequest { Token = second });
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "assignments-integration-test");
        return client;
    }

    private static string Recruiter()
        => JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite);

    private static CreateAssignmentRequest ValidCreate()
        => new()
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = DevelopmentQuizSeeder.SampleQuizBackend,
            CandidateEmail = $"candidate.{Guid.NewGuid():N}@example.com",
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 },
            AttemptLimit = 1
        };
}
