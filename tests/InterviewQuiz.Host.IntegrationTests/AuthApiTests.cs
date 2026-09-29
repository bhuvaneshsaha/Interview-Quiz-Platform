using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Infrastructure.Seeding;

namespace InterviewQuiz.Host.IntegrationTests;

[Collection("Database")]
public sealed class AuthApiTests
{
    private readonly InterviewQuizWebApplicationFactory _factory;

    public AuthApiTests(InterviewQuizWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RequiresDatabaseFact]
    public async Task Login_success_then_me()
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
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var me = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOptions);
        Assert.NotNull(profile);
        Assert.Equal(DevelopmentAccessSeeder.AdminEmail, profile!.Email);
        Assert.Contains("roles.manage", profile.Permissions);

        var permissions = await client.GetFromJsonAsync<List<string>>("/api/me/permissions", JsonOptions);
        Assert.NotNull(permissions);
        Assert.Contains("users.manage", permissions);
    }

    [RequiresDatabaseFact]
    public async Task Login_failure_is_unauthorized()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = DevelopmentAccessSeeder.AdminEmail,
            Password = "Wrong.Password!1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Refresh_rotates_token()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = DevelopmentAccessSeeder.RecruiterEmail,
            Password = DevelopmentAccessSeeder.RecruiterPassword
        });
        var original = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(original);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest
        {
            RefreshToken = original!.RefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(rotated);
        Assert.NotEqual(original.RefreshToken, rotated!.RefreshToken);

        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest
        {
            RefreshToken = original.RefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }
}
