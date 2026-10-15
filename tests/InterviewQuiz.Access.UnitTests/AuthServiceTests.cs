using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure.Identity;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Access.UnitTests;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Login_succeeds_with_valid_password()
    {
        var harness = Harness.Create([PermissionCodes.Openings.Read]);
        var tokens = await harness.Auth.LoginAsync(
            new LoginRequest { Email = "recruiter.dev@example.com", Password = "secret" },
            CancellationToken.None);

        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal("Bearer", tokens.TokenType);
    }

    [Fact]
    public async Task Login_fails_with_wrong_password()
    {
        var harness = Harness.Create([PermissionCodes.Openings.Read]);
        var tokens = await harness.Auth.LoginAsync(
            new LoginRequest { Email = "recruiter.dev@example.com", Password = "nope" },
            CancellationToken.None);

        Assert.Null(tokens);
        Assert.Equal(1, harness.Users.AccessFailedCount);
    }

    [Fact]
    public async Task Login_fails_when_user_is_locked_out()
    {
        var harness = Harness.Create([PermissionCodes.Openings.Read], lockedOut: true);
        var tokens = await harness.Auth.LoginAsync(
            new LoginRequest { Email = "recruiter.dev@example.com", Password = "secret" },
            CancellationToken.None);

        Assert.Null(tokens);
        Assert.Equal(0, harness.Users.AccessFailedCount);
    }

    [Fact]
    public async Task Login_fails_when_user_is_disabled()
    {
        var harness = Harness.Create([PermissionCodes.Openings.Read], disabled: true);
        var tokens = await harness.Auth.LoginAsync(
            new LoginRequest { Email = "recruiter.dev@example.com", Password = "secret" },
            CancellationToken.None);

        Assert.Null(tokens);
    }

    [Fact]
    public async Task Refresh_re_reads_permissions_from_store()
    {
        var harness = Harness.Create([PermissionCodes.Openings.Read]);
        var login = await harness.Auth.LoginAsync(
            new LoginRequest { Email = "recruiter.dev@example.com", Password = "secret" },
            CancellationToken.None);
        Assert.NotNull(login);

        harness.Permissions.Codes = [PermissionCodes.Openings.Read, PermissionCodes.Openings.Write];

        var refreshed = await harness.Auth.RefreshAsync(
            new RefreshRequest { RefreshToken = login!.RefreshToken },
            CancellationToken.None);

        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken);

        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var jwt = CreateJwtOptions();
        var result = await handler.ValidateTokenAsync(refreshed.AccessToken, JwtTokenValidation.Create(jwt));
        Assert.True(result.IsValid, result.Exception?.ToString());
        var permissions = result.ClaimsIdentity!.FindAll(PermissionClaims.Permission).Select(c => c.Value).ToArray();
        Assert.Contains(PermissionCodes.Openings.Write, permissions);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var harness = Harness.Create([PermissionCodes.Openings.Read]);
        var login = await harness.Auth.LoginAsync(
            new LoginRequest { Email = "recruiter.dev@example.com", Password = "secret" },
            CancellationToken.None);
        Assert.NotNull(login);

        await harness.Auth.LogoutAsync(
            new RefreshRequest { RefreshToken = login!.RefreshToken },
            CancellationToken.None);

        var refreshed = await harness.Auth.RefreshAsync(
            new RefreshRequest { RefreshToken = login.RefreshToken },
            CancellationToken.None);
        Assert.Null(refreshed);
    }

    private static JwtOptions CreateJwtOptions()
        => new()
        {
            Issuer = "InterviewQuiz.Tests",
            Audience = "InterviewQuiz.Tests",
            SigningKey = "InterviewQuiz-Testing-Signing-Key-Not-For-Production!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        };

    private sealed class Harness
    {
        public required AuthService Auth { get; init; }
        public required FakePermissionReader Permissions { get; init; }
        public required FakeUsers Users { get; init; }

        public static Harness Create(IReadOnlyList<string> permissions, bool disabled = false, bool lockedOut = false)
        {
            var user = new ApplicationUser
            {
                Id = "user-1",
                Email = "recruiter.dev@example.com",
                UserName = "recruiter.dev@example.com",
                IsDisabled = disabled
            };
            var users = new FakeUsers(user, password: "secret") { LockedOut = lockedOut };
            var permissionReader = new FakePermissionReader(permissions);
            var refresh = new FakeRefreshStore();
            var issuer = new JwtAccessTokenIssuer(Options.Create(CreateJwtOptions()), new FixedClock(Now));
            var auth = new AuthService(users, permissionReader, issuer, refresh, NullLogger<AuthService>.Instance);
            return new Harness { Auth = auth, Permissions = permissionReader, Users = users };
        }
    }

    private sealed class FakeUsers : IIdentityUserDirectory
    {
        private readonly ApplicationUser _user;
        private readonly string _password;

        public FakeUsers(ApplicationUser user, string password)
        {
            _user = user;
            _password = password;
        }

        public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
            => Task.FromResult(string.Equals(email, _user.Email, StringComparison.OrdinalIgnoreCase) ? _user : null);

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult(_user.Id == userId ? _user : null);

        public int AccessFailedCount { get; private set; }

        public bool LockedOut { get; set; }

        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
            => Task.FromResult(password == _password);

        public Task<bool> IsLockedOutAsync(ApplicationUser user)
            => Task.FromResult(LockedOut);

        public Task AccessFailedAsync(ApplicationUser user)
        {
            AccessFailedCount++;
            return Task.CompletedTask;
        }

        public Task ResetAccessFailedCountAsync(ApplicationUser user)
        {
            AccessFailedCount = 0;
            return Task.CompletedTask;
        }

        public Task<IdentityCreateResult> CreateAsync(ApplicationUser user, string password)
            => throw new NotSupportedException();

        public Task<IdentityCreateResult> CreateWithoutPasswordAsync(ApplicationUser user)
            => throw new NotSupportedException();

        public Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        public Task<IReadOnlyList<ApplicationUser>> ListAsync(int skip, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ApplicationUser>>([_user]);
    }

    private sealed class FakePermissionReader : IEffectivePermissionReader
    {
        public FakePermissionReader(IReadOnlyList<string> codes) => Codes = codes;

        public IReadOnlyList<string> Codes { get; set; }

        public Task<IReadOnlyList<string>> GetAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult(Codes);
    }

    private sealed class FakeRefreshStore : IRefreshTokenStore
    {
        private readonly Dictionary<string, (string UserId, bool Revoked)> _tokens = new(StringComparer.Ordinal);

        public Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken cancellationToken)
        {
            var raw = RefreshTokenHasher.CreateToken();
            _tokens[raw] = (userId, false);
            return Task.FromResult(new IssuedRefreshToken(raw, Now.AddDays(7), Guid.NewGuid()));
        }

        public async Task<RefreshTokenRotation?> RotateAsync(string rawToken, CancellationToken cancellationToken)
        {
            if (!_tokens.TryGetValue(rawToken, out var existing) || existing.Revoked)
            {
                return null;
            }

            _tokens[rawToken] = (existing.UserId, true);
            var replacement = await IssueAsync(existing.UserId, cancellationToken);
            return new RefreshTokenRotation(existing.UserId, replacement);
        }

        public Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
        {
            if (_tokens.TryGetValue(rawToken, out var existing))
            {
                _tokens[rawToken] = (existing.UserId, true);
            }

            return Task.CompletedTask;
        }

        public Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken)
        {
            foreach (var pair in _tokens.Where(item => item.Value.UserId == userId).ToList())
            {
                _tokens[pair.Key] = (userId, true);
            }

            return Task.CompletedTask;
        }
    }
}
