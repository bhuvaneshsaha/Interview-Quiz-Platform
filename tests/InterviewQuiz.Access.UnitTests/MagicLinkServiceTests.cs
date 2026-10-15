using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure.Identity;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace InterviewQuiz.Access.UnitTests;

public sealed class MagicLinkServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid AssignmentId = Guid.Parse("7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001");

    [Fact]
    public async Task Consume_valid_invite_returns_candidate_jwt_without_refresh_token()
    {
        var harness = Harness.Create();
        var raw = await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);

        var response = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = raw },
            CancellationToken.None);

        Assert.Equal(AssignmentId, response.AssignmentId);
        Assert.Equal("Bearer", response.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal(Now.AddMinutes(60), response.AccessTokenExpiresAt);

        var json = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.DoesNotContain("refresh", json, StringComparison.OrdinalIgnoreCase);

        var claims = await ReadClaimsAsync(response.AccessToken);
        Assert.Equal(
            [PermissionCodes.Candidate.AttemptParticipate],
            claims.Permissions);
        Assert.Equal(AssignmentId.ToString("D"), claims.AssignmentId);
        Assert.Null(claims.AttemptId);
        Assert.Equal("candidate.dev@example.com", claims.Email);
        Assert.False(string.IsNullOrWhiteSpace(claims.Subject));
    }

    [Fact]
    public async Task Consume_after_rotate_rejects_old_token()
    {
        var harness = Harness.Create();
        var first = await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);
        var second = await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);
        Assert.NotEqual(first, second);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            harness.MagicLinks.ConsumeAsync(
                new ConsumeMagicLinkRequest { Token = first },
                CancellationToken.None));
        Assert.Equal(MagicLinkService.RevokedInviteMessage, ex.Message);

        var response = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = second },
            CancellationToken.None);
        Assert.Equal(AssignmentId, response.AssignmentId);
    }

    [Fact]
    public async Task Consume_after_submit_still_issues_a_view_token()
    {
        var harness = Harness.Create(status: "submitted");
        var raw = await IssueBypassingRulesAsync(harness);

        var response = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = raw },
            CancellationToken.None);

        Assert.Equal(AssignmentId, response.AssignmentId);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
    }

    [Fact]
    public async Task Consume_live_mode_returns_a_candidate_token()
    {
        var harness = Harness.Create(mode: "live");
        var raw = await IssueBypassingRulesAsync(harness);

        var response = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = raw },
            CancellationToken.None);

        Assert.Equal(AssignmentId, response.AssignmentId);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
    }

    [Fact]
    public async Task Consume_unknown_assignment_is_invalid_without_probing()
    {
        var harness = Harness.Create();
        harness.Assignments.Info = null;
        var raw = await IssueBypassingRulesAsync(harness);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            harness.MagicLinks.ConsumeAsync(
                new ConsumeMagicLinkRequest { Token = raw },
                CancellationToken.None));
        Assert.Equal(MagicLinkService.InvalidInviteMessage, ex.Message);
    }

    [Fact]
    public async Task Employee_email_as_candidate_gets_only_candidate_permission()
    {
        var recruiter = new ApplicationUser
        {
            Id = "recruiter-1",
            Email = "recruiter.dev@example.com",
            UserName = "recruiter.dev@example.com"
        };
        var harness = Harness.Create(
            email: recruiter.Email,
            existingUsers: [recruiter]);
        var raw = await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);

        var response = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = raw },
            CancellationToken.None);

        var claims = await ReadClaimsAsync(response.AccessToken);
        Assert.Equal("recruiter-1", claims.Subject);
        Assert.Equal(
            [PermissionCodes.Candidate.AttemptParticipate],
            claims.Permissions);
        Assert.DoesNotContain(PermissionCodes.Delivery.AssignmentsWrite, claims.Permissions);
        Assert.DoesNotContain(PermissionCodes.Openings.Write, claims.Permissions);
        Assert.Equal(AssignmentId.ToString("D"), claims.AssignmentId);
    }

    [Fact]
    public async Task Reconsume_same_invite_while_invitable_issues_new_access_token()
    {
        var harness = Harness.Create();
        var raw = await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);

        var first = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = raw },
            CancellationToken.None);
        var second = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = raw },
            CancellationToken.None);

        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(AssignmentId, second.AssignmentId);
        var claims = await ReadClaimsAsync(second.AccessToken);
        Assert.Equal([PermissionCodes.Candidate.AttemptParticipate], claims.Permissions);
    }

    [Fact]
    public async Task Issue_allows_live_mode()
    {
        var harness = Harness.Create(mode: "live");
        var raw = await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(raw));
    }

    [Theory]
    [InlineData("pendingReview")]
    [InlineData("completed")]
    public async Task Issue_rejects_terminal_assignment(string status)
    {
        var harness = Harness.Create(status: status);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None));
        Assert.Equal(MagicLinkService.NoLongerInvitableMessage, ex.Message);
    }

    [Fact]
    public void BuildInviteUrl_uses_public_base_url()
    {
        var harness = Harness.Create();
        var url = harness.MagicLinks.BuildInviteUrl("abc+def");
        Assert.Equal("http://localhost:4200/attempt?token=abc%2Bdef", url);
    }

    [Fact]
    public async Task Consume_includes_attempt_id_when_lookup_returns_one()
    {
        var attemptId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var harness = Harness.Create(attemptId: attemptId);
        var raw = await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);
        var response = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = raw },
            CancellationToken.None);

        var claims = await ReadClaimsAsync(response.AccessToken);
        Assert.Equal(attemptId.ToString("D"), claims.AttemptId);
    }

    [Fact]
    public async Task Consume_rejects_expired_invite()
    {
        var harness = Harness.Create();
        var raw = await harness.MagicLinks.IssueAsync(
            AssignmentId,
            CancellationToken.None,
            Now.AddHours(1));
        harness.Invites.Now = Now.AddHours(2);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            harness.MagicLinks.ConsumeAsync(
                new ConsumeMagicLinkRequest { Token = raw },
                CancellationToken.None));
        Assert.Equal(MagicLinkService.ExpiredInviteMessage, ex.Message);
    }

    [Fact]
    public async Task Revoke_does_not_rotate_and_blocks_consume()
    {
        var harness = Harness.Create();
        var raw = await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);
        await harness.MagicLinks.RevokeAsync(AssignmentId, CancellationToken.None);

        var status = await harness.MagicLinks.GetStatusAsync(AssignmentId, CancellationToken.None);
        Assert.Equal(MagicLinkInviteStatus.Revoked, status.State);
        Assert.Equal(1, harness.Invites.IssueCount);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            harness.MagicLinks.ConsumeAsync(
                new ConsumeMagicLinkRequest { Token = raw },
                CancellationToken.None));
        Assert.Equal(MagicLinkService.RevokedInviteMessage, ex.Message);
    }

    [Fact]
    public async Task Extend_keeps_the_same_token()
    {
        var harness = Harness.Create();
        var raw = await harness.MagicLinks.IssueAsync(
            AssignmentId,
            CancellationToken.None,
            Now.AddHours(1));
        var extendedTo = Now.AddDays(3);

        var status = await harness.MagicLinks.ExtendAsync(AssignmentId, extendedTo, CancellationToken.None);

        Assert.Equal(MagicLinkInviteStatus.Active, status.State);
        Assert.Equal(extendedTo, status.ExpiresAt);
        Assert.Equal(1, harness.Invites.IssueCount);
        var response = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = raw },
            CancellationToken.None);
        Assert.Equal(AssignmentId, response.AssignmentId);
    }

    [Fact]
    public async Task Issue_applies_configured_default_lifetime_when_expiry_omitted()
    {
        var harness = Harness.Create(defaultLifetimeHours: 48);
        await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);

        var status = await harness.MagicLinks.GetStatusAsync(AssignmentId, CancellationToken.None);
        Assert.Equal(MagicLinkInviteStatus.Active, status.State);
        Assert.Equal(Now.AddHours(48), status.ExpiresAt);
    }

    [Fact]
    public async Task Issue_and_consume_logs_do_not_contain_the_raw_token()
    {
        var logs = new ListLogger<MagicLinkService>();
        var harness = Harness.Create(logger: logs);
        var raw = await harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None);
        await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = raw },
            CancellationToken.None);

        var written = string.Join('\n', logs.Messages);
        Assert.DoesNotContain(raw, written, StringComparison.Ordinal);
        Assert.DoesNotContain("token=", written, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> IssueBypassingRulesAsync(Harness harness)
        => await harness.Invites.RotateAsync(AssignmentId, null, CancellationToken.None);

    private static async Task<TokenClaims> ReadClaimsAsync(string token)
    {
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, JwtTokenValidation.Create(CreateJwtOptions()));
        Assert.True(result.IsValid, result.Exception?.ToString());
        var identity = result.ClaimsIdentity!;
        return new TokenClaims(
            identity.FindFirst("sub")?.Value ?? identity.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            identity.FindFirst("email")?.Value ?? identity.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            identity.FindAll(PermissionClaims.Permission).Select(c => c.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray(),
            identity.FindFirst(PermissionClaims.AssignmentId)?.Value,
            identity.FindFirst(PermissionClaims.AttemptId)?.Value);
    }

    private static JwtOptions CreateJwtOptions()
        => new()
        {
            Issuer = "InterviewQuiz.Tests",
            Audience = "InterviewQuiz.Tests",
            SigningKey = "InterviewQuiz-Testing-Signing-Key-Not-For-Production!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
            CandidateAccessTokenMinutes = 60
        };

    private sealed record TokenClaims(
        string? Subject,
        string? Email,
        IReadOnlyList<string> Permissions,
        string? AssignmentId,
        string? AttemptId);

    private sealed class Harness
    {
        public required MagicLinkService MagicLinks { get; init; }
        public required FakeAssignmentInviteInfo Assignments { get; init; }
        public required FakeInviteStore Invites { get; init; }

        public static Harness Create(
            string email = "candidate.dev@example.com",
            string mode = "async",
            string status = "notStarted",
            Guid? attemptId = null,
            IReadOnlyList<ApplicationUser>? existingUsers = null,
            int defaultLifetimeHours = 0,
            ILogger<MagicLinkService>? logger = null)
        {
            var assignments = new FakeAssignmentInviteInfo(new AssignmentInviteInfoDto(
                AssignmentId,
                email,
                mode,
                status));
            var invites = new FakeInviteStore { Now = Now };
            var users = new FakeUsers(existingUsers ?? []);
            var issuer = new JwtAccessTokenIssuer(Options.Create(CreateJwtOptions()), new FixedClock(Now));
            IEnumerable<ICandidateAttemptIdLookup> lookups = attemptId is { } id
                ? [new FakeAttemptLookup(id)]
                : [];
            var magic = new MagicLinkService(
                assignments,
                invites,
                users,
                issuer,
                lookups,
                new FixedClock(Now),
                Options.Create(new InviteOptions { DefaultLifetimeHours = defaultLifetimeHours }),
                Options.Create(new PublicBaseUrlOptions { PublicBaseUrl = "http://localhost:4200/" }),
                logger ?? NullLogger<MagicLinkService>.Instance);
            return new Harness { MagicLinks = magic, Assignments = assignments, Invites = invites };
        }
    }

    private sealed class FakeAssignmentInviteInfo : IAssignmentInviteInfo
    {
        public FakeAssignmentInviteInfo(AssignmentInviteInfoDto? info) => Info = info;

        public AssignmentInviteInfoDto? Info { get; set; }

        public Task<AssignmentInviteInfoDto?> GetAsync(Guid assignmentId, CancellationToken cancellationToken)
            => Task.FromResult(Info is not null && Info.AssignmentId == assignmentId ? Info : null);
    }

    private sealed class FakeInviteStore : IMagicLinkInviteStore
    {
        private readonly Dictionary<string, (Guid AssignmentId, DateTimeOffset? ExpiresAt, bool Revoked)> _byRaw =
            new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, string> _rawByAssignment = [];

        public DateTimeOffset Now { get; set; }
        public int IssueCount { get; private set; }

        public Task<string> RotateAsync(Guid assignmentId, DateTimeOffset? expiresAt, CancellationToken cancellationToken)
        {
            if (_rawByAssignment.TryGetValue(assignmentId, out var previous)
                && _byRaw.TryGetValue(previous, out var prior))
            {
                _byRaw[previous] = prior with { Revoked = true };
            }

            var raw = RefreshTokenHasher.CreateToken();
            _byRaw[raw] = (assignmentId, expiresAt, false);
            _rawByAssignment[assignmentId] = raw;
            IssueCount++;
            return Task.FromResult(raw);
        }

        public Task<MagicLinkLookup> FindAsync(string rawToken, CancellationToken cancellationToken)
        {
            if (!_byRaw.TryGetValue(rawToken, out var row))
            {
                return Task.FromResult(MagicLinkLookup.Unknown);
            }

            if (row.Revoked)
            {
                return Task.FromResult(new MagicLinkLookup(MagicLinkLookupKind.Revoked, row.AssignmentId));
            }

            if (row.ExpiresAt is { } expiresAt && expiresAt <= Now)
            {
                return Task.FromResult(new MagicLinkLookup(MagicLinkLookupKind.Expired, row.AssignmentId));
            }

            return Task.FromResult(new MagicLinkLookup(MagicLinkLookupKind.Active, row.AssignmentId));
        }

        public Task<MagicLinkInviteStatus> GetStatusAsync(Guid assignmentId, CancellationToken cancellationToken)
        {
            if (!_rawByAssignment.TryGetValue(assignmentId, out var raw) || !_byRaw.TryGetValue(raw, out var row))
            {
                return Task.FromResult(MagicLinkInviteStatus.Missing);
            }

            if (row.Revoked)
            {
                return Task.FromResult(new MagicLinkInviteStatus(MagicLinkInviteStatus.Revoked, null));
            }

            var expired = row.ExpiresAt is { } expiresAt && expiresAt <= Now;
            return Task.FromResult(new MagicLinkInviteStatus(
                expired ? MagicLinkInviteStatus.Expired : MagicLinkInviteStatus.Active,
                row.ExpiresAt));
        }

        public Task RevokeCurrentAsync(Guid assignmentId, CancellationToken cancellationToken)
        {
            if (!_rawByAssignment.TryGetValue(assignmentId, out var raw)
                || !_byRaw.TryGetValue(raw, out var row)
                || row.Revoked)
            {
                throw new DomainException(MagicLinkInviteStore.NoInviteToRevokeMessage);
            }

            _byRaw[raw] = row with { Revoked = true };
            return Task.CompletedTask;
        }

        public Task<MagicLinkInviteStatus> ExtendCurrentAsync(
            Guid assignmentId,
            DateTimeOffset? expiresAt,
            CancellationToken cancellationToken)
        {
            if (expiresAt is { } value && value <= Now)
            {
                throw new DomainException(MagicLinkInviteStore.ExpiryMustBeFutureMessage);
            }

            if (!_rawByAssignment.TryGetValue(assignmentId, out var raw)
                || !_byRaw.TryGetValue(raw, out var row)
                || row.Revoked)
            {
                throw new DomainException(MagicLinkInviteStore.NoInviteToExtendMessage);
            }

            _byRaw[raw] = row with { ExpiresAt = expiresAt };
            var expired = expiresAt is { } at && at <= Now;
            return Task.FromResult(new MagicLinkInviteStatus(
                expired ? MagicLinkInviteStatus.Expired : MagicLinkInviteStatus.Active,
                expiresAt));
        }

        public Task<string?> ReadCurrentTokenAsync(Guid assignmentId, CancellationToken cancellationToken)
        {
            if (!_rawByAssignment.TryGetValue(assignmentId, out var raw)
                || !_byRaw.TryGetValue(raw, out var row)
                || row.Revoked)
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(raw);
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }

    private sealed class FakeAttemptLookup : ICandidateAttemptIdLookup
    {
        private readonly Guid _attemptId;

        public FakeAttemptLookup(Guid attemptId) => _attemptId = attemptId;

        public Task<Guid?> GetAttemptIdAsync(Guid assignmentId, CancellationToken cancellationToken)
            => Task.FromResult<Guid?>(_attemptId);
    }

    private sealed class FakeUsers : IIdentityUserDirectory
    {
        private readonly List<ApplicationUser> _users;

        public FakeUsers(IReadOnlyList<ApplicationUser> users) => _users = users.ToList();

        public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
            => Task.FromResult(_users.FirstOrDefault(user =>
                string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult(_users.FirstOrDefault(user => user.Id == userId));

        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password) => Task.FromResult(false);

        public Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(false);

        public Task AccessFailedAsync(ApplicationUser user) => Task.CompletedTask;

        public Task ResetAccessFailedCountAsync(ApplicationUser user) => Task.CompletedTask;

        public Task<IdentityCreateResult> CreateAsync(ApplicationUser user, string password)
            => throw new NotSupportedException();

        public Task<IdentityCreateResult> CreateWithoutPasswordAsync(ApplicationUser user)
        {
            if (_users.Any(existing =>
                    string.Equals(existing.Email, user.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(IdentityCreateResult.Fail(["duplicate"]));
            }

            if (string.IsNullOrWhiteSpace(user.Id))
            {
                user.Id = Guid.NewGuid().ToString("D");
            }

            _users.Add(user);
            return Task.FromResult(IdentityCreateResult.Ok());
        }

        public Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(_users.Count);

        public Task<IReadOnlyList<ApplicationUser>> ListAsync(int skip, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ApplicationUser>>(_users.Skip(skip).Take(take).ToArray());
    }
}
