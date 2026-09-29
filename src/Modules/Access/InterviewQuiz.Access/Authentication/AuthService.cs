using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Infrastructure.Identity;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Access.Authentication;

public sealed class AuthService : IAuthService
{
    private readonly IIdentityUserDirectory _users;
    private readonly IEffectivePermissionReader _permissions;
    private readonly IJwtAccessTokenIssuer _accessTokens;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IIdentityUserDirectory users,
        IEffectivePermissionReader permissions,
        IJwtAccessTokenIssuer accessTokens,
        IRefreshTokenStore refreshTokens,
        ILogger<AuthService> logger)
    {
        _users = users;
        _permissions = permissions;
        _accessTokens = accessTokens;
        _refreshTokens = refreshTokens;
        _logger = logger;
    }

    public async Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var user = await _users.FindByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null || user.IsDisabled || !await _users.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogInformation("Login failed");
            return null;
        }

        _logger.LogInformation("Login succeeded for user {UserId}", user.Id);
        return await IssueSessionAsync(user.Id, user.Email!, cancellationToken);
    }

    public async Task<TokenResponse?> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var rotation = await _refreshTokens.RotateAsync(request.RefreshToken, cancellationToken);
        if (rotation is null)
        {
            return null;
        }

        var user = await _users.FindByIdAsync(rotation.UserId, cancellationToken);
        if (user is null || user.IsDisabled)
        {
            await _refreshTokens.RevokeAllForUserAsync(rotation.UserId, cancellationToken);
            return null;
        }

        var permissions = await _permissions.GetAsync(user.Id, cancellationToken);
        var access = _accessTokens.Issue(user.Id, user.Email!, permissions);
        return new TokenResponse(
            access.Token,
            rotation.Replacement.RawToken,
            access.ExpiresAt,
            rotation.Replacement.ExpiresAt);
    }

    public Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken)
        => _refreshTokens.RevokeAsync(request.RefreshToken, cancellationToken);

    private async Task<TokenResponse> IssueSessionAsync(
        string userId,
        string email,
        CancellationToken cancellationToken)
    {
        var permissions = await _permissions.GetAsync(userId, cancellationToken);
        var access = _accessTokens.Issue(userId, email, permissions);
        var refresh = await _refreshTokens.IssueAsync(userId, cancellationToken);
        return new TokenResponse(access.Token, refresh.RawToken, access.ExpiresAt, refresh.ExpiresAt);
    }
}
