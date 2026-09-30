using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure.Identity;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Permissions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace InterviewQuiz.Access.UnitTests;

public sealed class MagicLinkServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
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
        Assert.Equal(MagicLinkService.InvalidInviteMessage, ex.Message);

        var response = await harness.MagicLinks.ConsumeAsync(
            new ConsumeMagicLinkRequest { Token = second },
            CancellationToken.None);
        Assert.Equal(AssignmentId, response.AssignmentId);
    }

    [Fact]
    public async Task Consume_when_not_invitable_is_invalid()
    {
        var harness = Harness.Create(status: "submitted");
        var raw = await IssueBypassingRulesAsync(harness);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            harness.MagicLinks.ConsumeAsync(
                new ConsumeMagicLinkRequest { Token = raw },
                CancellationToken.None));
        Assert.Equal(MagicLinkService.InvalidInviteMessage, ex.Message);
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
    public async Task Issue_rejects_live_mode()
    {
        var harness = Harness.Create(mode: "live");
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            harness.MagicLinks.IssueAsync(AssignmentId, CancellationToken.None));
        Assert.Equal(MagicLinkService.LiveModeMessage, ex.Message);
    }

    [Fact]
    public async Task Issue_rejects_submitted_assignment()
    {
        var harness = Harness.Create(status: "submitted");
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

    private static async Task<string> IssueBypassingRulesAsync(Harness harness)
        => await harness.Invites.RotateAsync(AssignmentId, CancellationToken.None);

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
            IReadOnlyList<ApplicationUser>? existingUsers = null)
        {
            var assignments = new FakeAssignmentInviteInfo(new AssignmentInviteInfoDto(
                AssignmentId,
                email,
                mode,
                status));
            var invites = new FakeInviteStore();
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
                Options.Create(new PublicBaseUrlOptions { PublicBaseUrl = "http://localhost:4200/" }),
                NullLogger<MagicLinkService>.Instance);
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
        private readonly Dictionary<string, Guid> _activeByRaw = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, string> _rawByAssignment = [];

        public Task<string> RotateAsync(Guid assignmentId, CancellationToken cancellationToken)
        {
            if (_rawByAssignment.TryGetValue(assignmentId, out var previous))
            {
                _activeByRaw.Remove(previous);
            }

            var raw = RefreshTokenHasher.CreateToken();
            _activeByRaw[raw] = assignmentId;
            _rawByAssignment[assignmentId] = raw;
            return Task.FromResult(raw);
        }

        public Task<Guid?> FindActiveAssignmentIdAsync(string rawToken, CancellationToken cancellationToken)
            => Task.FromResult(
                _activeByRaw.TryGetValue(rawToken, out var assignmentId) ? assignmentId : (Guid?)null);
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
