using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Host.Mail;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class InviteAndTemplateAssignApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public InviteAndTemplateAssignApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Async_create_sends_email_and_sets_a_future_expiry()
    {
        var mail = Mailer();
        mail.Clear();
        var client = RecruiterClient();
        var post = await client.PostAsJsonAsync("/api/assignments", ValidCreate());
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(AssignmentEmail.Sent, created!.InviteEmailStatus);
        Assert.Equal(1, created.InviteEmailAttemptCount);
        Assert.Equal("active", created.InviteState);
        Assert.NotNull(created.InviteExpiresAt);
        Assert.True(created.InviteExpiresAt > DateTimeOffset.UtcNow.AddHours(24));

        var sent = Assert.Single(mail.Messages, message => message.AssignmentId == created.Id);
        Assert.Equal(created.CandidateEmail, sent.ToEmail);
        Assert.Contains(created.InviteUrl!, sent.InviteUrl, StringComparison.Ordinal);
        Assert.False(sent.Resend);

        var loaded = await (await client.GetAsync($"/api/assignments/{created.Id}"))
            .Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.Null(loaded!.InviteUrl);
        Assert.Equal(AssignmentEmail.Sent, loaded.InviteEmailStatus);
        Assert.Equal("active", loaded.InviteState);
    }

    [RequiresDatabaseFact]
    public async Task Failed_send_is_recorded_and_resend_uses_the_same_link()
    {
        var mail = Mailer();
        mail.Clear();
        mail.FailNext = true;
        var client = RecruiterClient();
        var created = await (await client.PostAsJsonAsync("/api/assignments", ValidCreate()))
            .Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.Equal(AssignmentEmail.Failed, created!.InviteEmailStatus);
        Assert.False(string.IsNullOrWhiteSpace(created.InviteUrl));
        Assert.DoesNotContain("token=", created.InviteEmailLastError ?? "", StringComparison.OrdinalIgnoreCase);

        var resend = await client.PostAsync($"/api/assignments/{created.Id}/invite/resend", null);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        var delivery = await resend.Content.ReadFromJsonAsync<InviteDeliveryResponse>(JsonOptions);
        Assert.Equal(AssignmentEmail.Resent, delivery!.InviteEmailStatus);
        Assert.Equal(2, delivery.InviteEmailAttemptCount);
        Assert.Null(delivery.InviteEmailLastError);

        var sent = Assert.Single(mail.Messages, message => message.AssignmentId == created.Id);
        Assert.True(sent.Resend);
        Assert.Equal(created.InviteUrl, sent.InviteUrl);

        var anonymous = _factory.CreateClient();
        var consume = await anonymous.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new InterviewQuiz.Access.Application.Contracts.ConsumeMagicLinkRequest
            {
                Token = MagicLinkApiTests.TokenFromInviteUrl(created.InviteUrl!)
            });
        Assert.Equal(HttpStatusCode.OK, consume.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Expired_invite_cannot_be_consumed_until_it_is_extended()
    {
        var client = RecruiterClient();
        var created = await (await client.PostAsJsonAsync("/api/assignments", ValidCreate()))
            .Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        var token = MagicLinkApiTests.TokenFromInviteUrl(created!.InviteUrl!);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            var invite = await db.MagicLinkInvites.SingleAsync(row =>
                row.AssignmentId == created.Id && row.RevokedAt == null);
            invite.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var anonymous = _factory.CreateClient();
        var expired = await anonymous.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new InterviewQuiz.Access.Application.Contracts.ConsumeMagicLinkRequest { Token = token });
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        Assert.Contains(
            "This invite has expired.",
            await expired.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var extend = await client.PostAsJsonAsync(
            $"/api/assignments/{created.Id}/invite/extend",
            new ExtendInviteRequest { ExpiresAt = DateTimeOffset.UtcNow.AddDays(2) });
        Assert.Equal(HttpStatusCode.OK, extend.StatusCode);
        var lifetime = await extend.Content.ReadFromJsonAsync<InviteLifetimeResponse>(JsonOptions);
        Assert.Equal("active", lifetime!.InviteState);

        var again = await anonymous.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new InterviewQuiz.Access.Application.Contracts.ConsumeMagicLinkRequest { Token = token });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Revoke_blocks_the_current_link_without_issuing_another()
    {
        var client = RecruiterClient();
        var created = await (await client.PostAsJsonAsync("/api/assignments", ValidCreate()))
            .Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        var token = MagicLinkApiTests.TokenFromInviteUrl(created!.InviteUrl!);

        var revoke = await client.PostAsync($"/api/assignments/{created.Id}/invite/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        var lifetime = await revoke.Content.ReadFromJsonAsync<InviteLifetimeResponse>(JsonOptions);
        Assert.Equal("revoked", lifetime!.InviteState);
        Assert.DoesNotContain("inviteUrl", await revoke.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var anonymous = _factory.CreateClient();
        var consume = await anonymous.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new InterviewQuiz.Access.Application.Contracts.ConsumeMagicLinkRequest { Token = token });
        Assert.Equal(HttpStatusCode.BadRequest, consume.StatusCode);
        Assert.Contains(
            "This invite has been revoked.",
            await consume.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var rows = await db.MagicLinkInvites.CountAsync(row => row.AssignmentId == created.Id);
        Assert.Equal(1, rows);
    }

    [RequiresDatabaseFact]
    public async Task Recruiter_assigns_from_template_without_quizzes_write()
    {
        var client = RecruiterClient(includeTemplatesRead: true);
        using var versionDoc = JsonDocument.Parse(await (await client.GetAsync(
            $"/api/templates/{DevelopmentQuizSeeder.SampleTemplateBackend}/versions/{DevelopmentQuizSeeder.SampleTemplateVersion1}"))
            .Content.ReadAsStringAsync());
        var questionCount = versionDoc.RootElement.GetProperty("questionCount").GetInt32();
        Assert.True(questionCount > 0);

        var post = await client.PostAsJsonAsync(
            "/api/assignments/from-template",
            new CreateAssignmentFromTemplateRequest
            {
                OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
                TemplateId = DevelopmentQuizSeeder.SampleTemplateBackend,
                TemplateVersionId = DevelopmentQuizSeeder.SampleTemplateVersion1,
                CandidateEmail = $"from-template.{Guid.NewGuid():N}@example.com",
                Mode = "async",
                Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 },
                AttemptLimit = 1,
                QuizTitle = "Assigned from template"
            });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.NotEqual(DevelopmentQuizSeeder.SampleQuizBackend, created!.QuizId);
        Assert.Equal(questionCount, created.SnapshotQuestionCount);
        Assert.Equal("Assigned from template", created.SnapshotTitle);
        Assert.False(string.IsNullOrWhiteSpace(created.InviteUrl));
        Assert.Equal(AssignmentEmail.Sent, created.InviteEmailStatus);

        using var quizDoc = JsonDocument.Parse(await (await client.GetAsync($"/api/quizzes/{created.QuizId}")).Content.ReadAsStringAsync());
        var quiz = quizDoc.RootElement;
        Assert.Equal(DevelopmentQuizSeeder.SampleTemplateBackend, quiz.GetProperty("originTemplateId").GetGuid());
        Assert.Equal(DevelopmentQuizSeeder.SampleTemplateVersion1, quiz.GetProperty("sourceTemplateVersionId").GetGuid());
        Assert.Equal(questionCount, quiz.GetProperty("questions").GetArrayLength());
    }

    [RequiresDatabaseFact]
    public async Task From_template_without_templates_read_is_forbidden()
    {
        var client = RecruiterClient(includeTemplatesRead: false);
        var response = await client.PostAsJsonAsync(
            "/api/assignments/from-template",
            new CreateAssignmentFromTemplateRequest
            {
                OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
                TemplateId = DevelopmentQuizSeeder.SampleTemplateBackend,
                CandidateEmail = "candidate@example.com",
                Mode = "async",
                Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 }
            });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private RecordingInviteMailer Mailer()
        => _factory.Services.GetRequiredService<RecordingInviteMailer>();

    private HttpClient RecruiterClient(bool includeTemplatesRead = false)
    {
        var permissions = new List<string>
        {
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite,
            PermissionCodes.Catalog.QuizzesRead
        };
        if (includeTemplatesRead)
        {
            permissions.Add(PermissionCodes.Catalog.TemplatesRead);
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestTokens.Create(permissions.ToArray()));
        return client;
    }

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

    private static class AssignmentEmail
    {
        public const string Sent = "sent";
        public const string Failed = "failed";
        public const string Resent = "resent";
    }
}
