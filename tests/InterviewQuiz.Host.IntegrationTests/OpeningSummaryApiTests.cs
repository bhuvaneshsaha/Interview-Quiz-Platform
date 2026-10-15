using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Delivery.Infrastructure.Seeding;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Infrastructure.Seeding;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class OpeningSummaryApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public OpeningSummaryApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Summary_lists_seed_quiz_and_assigned_candidate_without_attempts()
    {
        var client = CreateClient(
            PermissionCodes.Openings.Read,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Delivery.AssignmentsRead);

        var response = await client.GetAsync($"/api/openings/{DevelopmentOpeningSeeder.SampleOpeningBackend}/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<OpeningSummaryResponse>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Contains(summary!.Quizzes!, quiz =>
            quiz.Id == DevelopmentQuizSeeder.SampleQuizBackend
            && quiz.QuestionCount >= 8
            && !string.IsNullOrWhiteSpace(quiz.Title));
        Assert.True(summary.Candidates!.Assigned >= 1);
        Assert.True(summary.Candidates.Appeared >= 0);
        Assert.True(summary.Candidates.Submitted >= 0);

        var quizCounts = summary.Quizzes!.Single(quiz => quiz.Id == DevelopmentQuizSeeder.SampleQuizBackend).Candidates;
        Assert.NotNull(quizCounts);
        Assert.True(quizCounts!.Assigned >= 1);

        var seed = await client.GetAsync($"/api/assignments/{DevelopmentAssignmentSeeder.SampleAssignment}");
        Assert.Equal(HttpStatusCode.OK, seed.StatusCode);

        var filtered = await client.GetFromJsonAsync<PagedResult<AssignmentSummaryResponse>>(
            $"/api/assignments?openingId={DevelopmentOpeningSeeder.SampleOpeningBackend}&quizId={DevelopmentQuizSeeder.SampleQuizBackend}&pageSize=100",
            JsonOptions);
        Assert.NotNull(filtered);
        Assert.True(filtered!.TotalCount >= 1);
        Assert.All(filtered.Items, item => Assert.Equal(DevelopmentQuizSeeder.SampleQuizBackend, item.QuizId));

        var excluded = await client.GetFromJsonAsync<PagedResult<AssignmentSummaryResponse>>(
            $"/api/assignments?openingId={DevelopmentOpeningSeeder.SampleOpeningBackend}&quizId={Guid.NewGuid()}",
            JsonOptions);
        Assert.NotNull(excluded);
        Assert.Equal(0, excluded!.TotalCount);
    }

    [RequiresDatabaseFact]
    public async Task Summary_omits_sections_the_caller_cannot_read_and_empty_openings_are_empty()
    {
        var openingsOnly = CreateClient(PermissionCodes.Openings.Read);
        var hidden = await openingsOnly.GetFromJsonAsync<OpeningSummaryResponse>(
            $"/api/openings/{DevelopmentOpeningSeeder.SampleOpeningBackend}/summary",
            JsonOptions);
        Assert.NotNull(hidden);
        Assert.Null(hidden!.Quizzes);
        Assert.Null(hidden.Candidates);

        var writer = CreateClient(PermissionCodes.Openings.Read, PermissionCodes.Openings.Write, PermissionCodes.Catalog.QuizzesRead, PermissionCodes.Delivery.AssignmentsRead);
        var created = await writer.PostAsJsonAsync("/api/openings", new
        {
            title = "Empty opening",
            jobDescription = "No quizzes yet.",
            owner = "owner@example.com",
            startDate = "2026-11-01",
            headcount = 1,
            expectedExperienceYears = 2,
            handlers = Array.Empty<string>(),
            tags = new Dictionary<string, string>()
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var opening = await created.Content.ReadFromJsonAsync<OpeningResponse>(JsonOptions);
        Assert.NotNull(opening);

        var summary = await writer.GetFromJsonAsync<OpeningSummaryResponse>(
            $"/api/openings/{opening!.Id}/summary",
            JsonOptions);
        Assert.NotNull(summary);
        Assert.Empty(summary!.Quizzes!);
        Assert.Equal(0, summary.Candidates!.Assigned);
        Assert.Equal(0, summary.Candidates.Appeared);
        Assert.Equal(0, summary.Candidates.Submitted);
    }

    private HttpClient CreateClient(params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestTokens.Create(permissions));
        return client;
    }
}
