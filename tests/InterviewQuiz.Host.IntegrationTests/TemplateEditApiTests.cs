using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Catalog.Infrastructure.Persistence;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Kernel.Users;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class TemplateEditApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public TemplateEditApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = CatalogJson.SerializerOptions;

    [RequiresDatabaseFact]
    public async Task Metadata_and_content_edits_leave_clones_and_assignment_snapshots_unchanged()
    {
        var client = CreateClient(
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Catalog.TemplatesRead,
            PermissionCodes.Catalog.TemplatesWrite,
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite);

        var create = await client.PostAsJsonAsync("/api/quizzes", new
        {
            openingId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            title = "Template edit source",
            description = "Keep clones stable.",
            expectedExperienceYears = 4,
            tags = new Dictionary<string, string> { ["Role"] = "Backend" },
            questions = new[]
            {
                new
                {
                    type = "trueFalse",
                    stem = "Snapshots stay frozen.",
                    scoringMode = "auto",
                    points = 1,
                    body = new { correct = true },
                    sourceQuestionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var quiz = await create.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        Assert.NotNull(quiz);

        var publish = await client.PostAsync($"/api/quizzes/{quiz!.Id}/publish-template", null);
        Assert.Equal(HttpStatusCode.Created, publish.StatusCode);
        var published = await publish.Content.ReadFromJsonAsync<PublishTemplateResponse>(JsonOptions);
        Assert.NotNull(published);

        var clone = await client.PostAsJsonAsync(
            $"/api/templates/{published!.TemplateId}/versions/{published.VersionId}/clone",
            new { openingId = DevelopmentOpeningSeeder.SampleOpeningBackend, title = "Cloned before edit" });
        Assert.Equal(HttpStatusCode.Created, clone.StatusCode);
        var cloned = await clone.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        Assert.NotNull(cloned);
        var clonedQuestionId = cloned!.Questions[0].Id;

        var assigned = await client.PostAsJsonAsync("/api/assignments", new
        {
            openingId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            quizId = cloned.Id,
            candidateEmail = $"snapshot-{Guid.NewGuid():N}@example.com",
            mode = "async",
            timing = new { overallDurationMinutes = 30 },
            attemptLimit = 1
        });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        var assignment = await assigned.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(assignment);
        Assert.Equal("Cloned before edit", assignment!.SnapshotTitle);
        Assert.Equal(1, assignment.SnapshotQuestionCount);

        var detail = await client.GetFromJsonAsync<TemplateResponse>(
            $"/api/templates/{published.TemplateId}",
            JsonOptions);
        Assert.NotNull(detail);

        var metadata = await client.PutAsJsonAsync(
            $"/api/templates/{published.TemplateId}/metadata",
            new
            {
                title = "Renamed after clone",
                description = "Metadata version reuses questions.",
                expectedExperienceYears = 5,
                tags = new Dictionary<string, string> { ["Role"] = "Backend" },
                rowVersion = detail!.RowVersion,
                changeNote = "Rename the template"
            });
        Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);
        var renamed = await metadata.Content.ReadFromJsonAsync<TemplateResponse>(JsonOptions);
        Assert.NotNull(renamed);
        Assert.Equal(2, renamed!.LatestVersionNumber);

        var content = await client.PutAsJsonAsync(
            $"/api/templates/{published.TemplateId}/content",
            new
            {
                title = "Renamed after clone",
                description = "Content version.",
                expectedExperienceYears = 5,
                tags = new Dictionary<string, string> { ["Role"] = "Backend" },
                rowVersion = renamed.RowVersion,
                changeNote = "Edit the stem",
                questions = new[]
                {
                    new
                    {
                        type = "trueFalse",
                        stem = "Edited stem",
                        scoringMode = "auto",
                        points = 1,
                        body = new { correct = true },
                        sourceQuestionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
                    }
                }
            });
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);

        var cloneAgain = await client.GetFromJsonAsync<QuizResponse>($"/api/quizzes/{cloned.Id}", JsonOptions);
        Assert.NotNull(cloneAgain);
        Assert.Equal("Cloned before edit", cloneAgain!.Title);
        Assert.Equal(clonedQuestionId, cloneAgain.Questions[0].Id);
        Assert.Equal("Snapshots stay frozen.", cloneAgain.Questions[0].Stem);

        var assignmentAgain = await client.GetFromJsonAsync<AssignmentResponse>(
            $"/api/assignments/{assignment.Id}",
            JsonOptions);
        Assert.NotNull(assignmentAgain);
        Assert.Equal(assignment.SnapshotTitle, assignmentAgain!.SnapshotTitle);
        Assert.Equal(assignment.SnapshotQuestionCount, assignmentAgain.SnapshotQuestionCount);
        Assert.Equal(assignment.SnapshotId, assignmentAgain.SnapshotId);

        var versions = await client.GetFromJsonAsync<PagedResult<TemplateVersionSummaryResponse>>(
            $"/api/templates/{published.TemplateId}/versions?page=1&pageSize=20",
            JsonOptions);
        Assert.NotNull(versions);
        Assert.Contains(versions!.Items, item => item.VersionNumber == 1 && item.ChangeNote == "Published from quiz");
        Assert.Contains(versions.Items, item => item.VersionNumber == 2 && item.ChangeNote == "Rename the template");
        var latest = versions.Items.Single(item => item.VersionNumber == 3);
        Assert.Equal("Edit the stem", latest.ChangeNote);
        Assert.False(string.IsNullOrWhiteSpace(latest.PublishedByUserId));

        var versionOne = await client.GetFromJsonAsync<TemplateVersionResponse>(
            $"/api/templates/{published.TemplateId}/versions/{published.VersionId}",
            JsonOptions);
        var versionThree = await client.GetFromJsonAsync<TemplateVersionResponse>(
            $"/api/templates/{published.TemplateId}/versions/{latest.Id}",
            JsonOptions);
        Assert.NotNull(versionOne);
        Assert.NotNull(versionThree);
        Assert.Equal("Snapshots stay frozen.", versionOne!.Questions[0].Stem);
        Assert.Equal("Edited stem", versionThree!.Questions[0].Stem);
        Assert.NotEqual(versionOne.Questions[0].Id, versionThree.Questions[0].Id);
        Assert.Equal(versionOne.Questions[0].SourceQuestionId, versionThree.Questions[0].SourceQuestionId);

        var versionTwoSummary = versions.Items.Single(item => item.VersionNumber == 2);
        var versionTwo = await client.GetFromJsonAsync<TemplateVersionResponse>(
            $"/api/templates/{published.TemplateId}/versions/{versionTwoSummary.Id}",
            JsonOptions);
        Assert.NotNull(versionTwo);
        Assert.Equal(versionOne.Questions[0].Id, versionTwo!.Questions[0].Id);
        Assert.Equal(versionOne.Questions[0].Stem, versionTwo.Questions[0].Stem);
        Assert.Equal(1, versionTwo.QuestionCount);
        Assert.False(string.IsNullOrWhiteSpace(versionTwo.PublishedByDisplayName));

        await using var scope = _factory.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var storedMetadata = await catalog.TemplateVersions
            .Include(version => version.Questions)
            .SingleAsync(version => version.Id == versionTwo.Id);
        Assert.Empty(storedMetadata.Questions);
        Assert.Equal(versionOne.Id, storedMetadata.ContentVersionId);
        var storedContent = await catalog.TemplateVersions
            .Include(version => version.Questions)
            .SingleAsync(version => version.Id == versionThree.Id);
        Assert.NotEmpty(storedContent.Questions);
        Assert.Null(storedContent.ContentVersionId);

        var reader = CreateClient(PermissionCodes.Catalog.TemplatesRead);
        var denied = await reader.PutAsJsonAsync(
            $"/api/templates/{published.TemplateId}/metadata",
            new
            {
                title = "Nope",
                expectedExperienceYears = 1,
                rowVersion = 1,
                changeNote = "no write"
            });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Version_history_shows_the_author_display_name()
    {
        var directory = CreateClient(PermissionCodes.Openings.Read);
        var people = await directory.GetFromJsonAsync<List<AssignableUserDto>>(
            "/api/users/assignable?q=author.dev@example.com",
            JsonOptions);
        var author = Assert.Single(people!, person => person.Email == DevelopmentAccessSeeder.AuthorEmail);
        Assert.False(string.IsNullOrWhiteSpace(author.Name));
        Assert.NotEqual(author.Id, author.Name);

        var client = CreateClientFor(author.Id,
            PermissionCodes.Catalog.QuizzesRead,
            PermissionCodes.Catalog.QuizzesWrite,
            PermissionCodes.Catalog.TemplatesRead,
            PermissionCodes.Catalog.TemplatesWrite);
        var create = await client.PostAsJsonAsync("/api/quizzes", new
        {
            openingId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            title = "Named author template",
            description = "Display name on the version.",
            expectedExperienceYears = 2,
            questions = new[]
            {
                new
                {
                    type = "trueFalse",
                    stem = "The author name is a person.",
                    scoringMode = "auto",
                    points = 1,
                    body = new { correct = true }
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var quiz = await create.Content.ReadFromJsonAsync<QuizResponse>(JsonOptions);
        var publish = await client.PostAsync($"/api/quizzes/{quiz!.Id}/publish-template", null);
        Assert.Equal(HttpStatusCode.Created, publish.StatusCode);
        var published = await publish.Content.ReadFromJsonAsync<PublishTemplateResponse>(JsonOptions);

        var versions = await client.GetFromJsonAsync<PagedResult<TemplateVersionSummaryResponse>>(
            $"/api/templates/{published!.TemplateId}/versions?page=1&pageSize=20",
            JsonOptions);
        var latest = Assert.Single(versions!.Items);
        Assert.Equal(author.Id, latest.PublishedByUserId);
        Assert.Equal(author.Name, latest.PublishedByDisplayName);
    }

    private HttpClient CreateClient(params string[] permissions)
        => CreateClientFor("integration-user", permissions);

    private HttpClient CreateClientFor(string userId, params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateForUser(userId, permissions));
        return client;
    }
}
