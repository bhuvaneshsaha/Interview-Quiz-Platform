using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class MagicLinkApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public MagicLinkApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Consume_valid_invite_returns_candidate_jwt_and_me()
    {
        var created = await CreateAsyncAssignmentAsync("candidate.dev@example.com");
        var raw = TokenFromInviteUrl(created.InviteUrl!);

        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = raw });
        Assert.Equal(HttpStatusCode.OK, consume.StatusCode);

        var payload = await consume.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        Assert.False(document.RootElement.TryGetProperty("refreshToken", out _));
        Assert.False(document.RootElement.TryGetProperty("refreshTokenExpiresAt", out _));

        var tokens = JsonSerializer.Deserialize<CandidateTokenResponse>(payload, JsonOptions);
        Assert.NotNull(tokens);
        Assert.Equal("Bearer", tokens!.TokenType);
        Assert.Equal(created.Id, tokens.AssignmentId);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));

        var claims = await ReadClaimsAsync(tokens.AccessToken);
        Assert.Equal([PermissionCodes.Candidate.AttemptParticipate], claims.Permissions);
        Assert.Equal(created.Id.ToString("D"), claims.AssignmentId);
        Assert.Null(claims.AttemptId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", JsonOptions);
        Assert.NotNull(me);
        Assert.Equal("candidate.dev@example.com", me!.Email);
        Assert.Equal([PermissionCodes.Candidate.AttemptParticipate], me.Permissions);

        var permissions = await client.GetFromJsonAsync<List<string>>("/api/me/permissions", JsonOptions);
        Assert.Equal([PermissionCodes.Candidate.AttemptParticipate], permissions);

        var forbidden = await client.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = DevelopmentQuizSeeder.SampleQuizBackend,
            CandidateEmail = "other@example.com",
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 }
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Consume_after_rotate_rejects_old_token()
    {
        var created = await CreateAsyncAssignmentAsync();
        var first = TokenFromInviteUrl(created.InviteUrl!);
        var second = await IssueAsync(created.Id);

        var client = _factory.CreateClient();
        var old = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = first });
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        var problem = await old.Content.ReadAsStringAsync();
        Assert.Contains("Invite is not valid.", problem, StringComparison.Ordinal);

        var current = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = second });
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Consume_when_not_invitable_is_bad_request()
    {
        var created = await CreateAsyncAssignmentAsync();
        var raw = TokenFromInviteUrl(created.InviteUrl!);

        using (var scope = _factory.Services.CreateScope())
        {
            var lifecycle = scope.ServiceProvider.GetRequiredService<IAssignmentLifecycle>();
            await lifecycle.NotifyAttemptSubmitted(
                created.Id,
                Guid.NewGuid(),
                AttemptResultStatuses.Complete,
                CancellationToken.None);
        }

        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = raw });
        Assert.Equal(HttpStatusCode.BadRequest, consume.StatusCode);
        Assert.Contains("Invite is not valid.", await consume.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [RequiresDatabaseFact]
    public async Task Employee_email_as_candidate_does_not_copy_recruiter_permissions()
    {
        var created = await CreateAsyncAssignmentAsync(DevelopmentAccessSeeder.RecruiterEmail);
        var raw = TokenFromInviteUrl(created.InviteUrl!);

        var client = _factory.CreateClient();
        var consume = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = raw });
        Assert.Equal(HttpStatusCode.OK, consume.StatusCode);
        var candidate = await consume.Content.ReadFromJsonAsync<CandidateTokenResponse>(JsonOptions);
        Assert.NotNull(candidate);

        var claims = await ReadClaimsAsync(candidate!.AccessToken);
        Assert.Equal([PermissionCodes.Candidate.AttemptParticipate], claims.Permissions);
        Assert.DoesNotContain(PermissionCodes.Delivery.AssignmentsWrite, claims.Permissions);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", candidate.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", JsonOptions);
        Assert.Equal([PermissionCodes.Candidate.AttemptParticipate], me!.Permissions);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = DevelopmentAccessSeeder.RecruiterEmail,
            Password = DevelopmentAccessSeeder.RecruiterPassword
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var employee = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(employee);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", employee!.AccessToken);
        var employeeMe = await client.GetFromJsonAsync<MeResponse>("/api/me", JsonOptions);
        Assert.Contains(PermissionCodes.Delivery.AssignmentsWrite, employeeMe!.Permissions);
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate, employeeMe.Permissions);
    }

    [RequiresDatabaseFact]
    public async Task Reconsume_same_invite_while_invitable_issues_new_access_token()
    {
        var created = await CreateAsyncAssignmentAsync();
        var raw = TokenFromInviteUrl(created.InviteUrl!);

        var client = _factory.CreateClient();
        var first = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = raw });
        var second = await client.PostAsJsonAsync(
            "/api/auth/magic-link/consume",
            new ConsumeMagicLinkRequest { Token = raw });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstTokens = await first.Content.ReadFromJsonAsync<CandidateTokenResponse>(JsonOptions);
        var secondTokens = await second.Content.ReadFromJsonAsync<CandidateTokenResponse>(JsonOptions);
        Assert.NotEqual(firstTokens!.AccessToken, secondTokens!.AccessToken);
        Assert.Equal(created.Id, secondTokens.AssignmentId);
    }

    [RequiresDatabaseFact]
    public async Task Get_consume_is_method_not_allowed()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/magic-link/consume");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.StatusCode is HttpStatusCode.MethodNotAllowed
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.NotFound,
            response.StatusCode.ToString());
    }

    [RequiresDatabaseFact]
    public async Task Login_still_issues_employee_refresh_token()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = DevelopmentAccessSeeder.AdminEmail,
            Password = DevelopmentAccessSeeder.AdminPassword
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens!.RefreshToken));
        Assert.DoesNotContain(PermissionCodes.Candidate.AttemptParticipate,
            (await ReadClaimsAsync(tokens.AccessToken)).Permissions);
    }

    private async Task<AssignmentResponse> CreateAsyncAssignmentAsync(
        string email = "candidate.dev@example.com")
    {
        var client = CreateAuthenticatedClient(Recruiter());
        var post = await client.PostAsJsonAsync("/api/assignments", new CreateAssignmentRequest
        {
            OpeningId = DevelopmentOpeningSeeder.SampleOpeningBackend,
            QuizId = DevelopmentQuizSeeder.SampleQuizBackend,
            CandidateEmail = email,
            Mode = "async",
            Timing = new AssignmentTimingRequest { OverallDurationMinutes = 30 },
            AttemptLimit = 1
        });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<AssignmentResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created!.InviteUrl));
        return created;
    }

    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static string Recruiter()
        => JwtTestTokens.Create(
            PermissionCodes.Delivery.AssignmentsRead,
            PermissionCodes.Delivery.AssignmentsWrite);

    private async Task<string> IssueAsync(Guid assignmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var magic = scope.ServiceProvider.GetRequiredService<IMagicLinkService>();
        return await magic.IssueAsync(assignmentId, CancellationToken.None);
    }

    internal static string TokenFromInviteUrl(string inviteUrl)
    {
        var uri = new Uri(inviteUrl);
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && string.Equals(pair[0], "token", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[1]);
            }
        }

        throw new InvalidOperationException("inviteUrl does not contain a token.");
    }

    private static async Task<TokenClaims> ReadClaimsAsync(string token)
    {
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, JwtTokenValidation.Create(new JwtOptions
        {
            Issuer = JwtTestTokens.Issuer,
            Audience = JwtTestTokens.Audience,
            SigningKey = JwtTestTokens.SigningKey,
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
            CandidateAccessTokenMinutes = 60
        }));
        Assert.True(result.IsValid, result.Exception?.ToString());
        var identity = result.ClaimsIdentity!;
        return new TokenClaims(
            identity.FindAll(PermissionClaims.Permission).Select(c => c.Value)
                .OrderBy(v => v, StringComparer.Ordinal).ToArray(),
            identity.FindFirst(PermissionClaims.AssignmentId)?.Value,
            identity.FindFirst(PermissionClaims.AttemptId)?.Value);
    }

    private sealed record TokenClaims(
        IReadOnlyList<string> Permissions,
        string? AssignmentId,
        string? AttemptId);
}
