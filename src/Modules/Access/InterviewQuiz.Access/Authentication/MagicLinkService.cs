using System.Diagnostics;
using InterviewQuiz.Access.Application.Contracts;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure.Identity;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Access.Authentication;

public sealed class MagicLinkService : IMagicLinkService
{
    public const string InvalidInviteMessage = "Invite is not valid.";
    public const string LiveModeMessage = "Invite links are for async assignments.";
    public const string NoLongerInvitableMessage = "Assignment is no longer invitable.";

    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Access");

    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "submitted",
        "pendingReview",
        "completed"
    };

    private readonly IAssignmentInviteInfo _assignments;
    private readonly IMagicLinkInviteStore _invites;
    private readonly IIdentityUserDirectory _users;
    private readonly IJwtAccessTokenIssuer _tokens;
    private readonly ICandidateAttemptIdLookup? _attempts;
    private readonly IClock _clock;
    private readonly PublicBaseUrlOptions _publicBaseUrl;
    private readonly ILogger<MagicLinkService> _logger;

    public MagicLinkService(
        IAssignmentInviteInfo assignments,
        IMagicLinkInviteStore invites,
        IIdentityUserDirectory users,
        IJwtAccessTokenIssuer tokens,
        IEnumerable<ICandidateAttemptIdLookup> attemptLookups,
        IClock clock,
        IOptions<PublicBaseUrlOptions> publicBaseUrl,
        ILogger<MagicLinkService> logger)
    {
        _assignments = assignments;
        _invites = invites;
        _users = users;
        _tokens = tokens;
        _attempts = attemptLookups.FirstOrDefault();
        _clock = clock;
        _publicBaseUrl = publicBaseUrl.Value;
        _logger = logger;
    }

    public async Task<string> IssueAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("auth.magic-link.issue");
        activity?.SetTag("assignment.id", assignmentId);

        var info = await _assignments.GetAsync(assignmentId, cancellationToken);
        EnsureIssuable(info);
        var raw = await _invites.RotateAsync(assignmentId, cancellationToken);
        _logger.LogInformation("Magic-link issued for assignment {AssignmentId}", assignmentId);
        return raw;
    }

    public async Task<CandidateTokenResponse> ConsumeAsync(
        ConsumeMagicLinkRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("auth.magic-link.consume");

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new DomainException(InvalidInviteMessage);
        }

        var assignmentId = await _invites.FindActiveAssignmentIdAsync(request.Token.Trim(), cancellationToken);
        if (assignmentId is null)
        {
            _logger.LogInformation("Magic-link consume rejected");
            throw new DomainException(InvalidInviteMessage);
        }

        activity?.SetTag("assignment.id", assignmentId.Value);

        var info = await _assignments.GetAsync(assignmentId.Value, cancellationToken);
        if (info is null || !IsInvitable(info))
        {
            _logger.LogInformation(
                "Magic-link consume rejected for assignment {AssignmentId}",
                assignmentId.Value);
            throw new DomainException(InvalidInviteMessage);
        }

        var user = await FindOrCreateCandidateAsync(info.CandidateEmail, assignmentId.Value, cancellationToken);
        Guid? attemptId = null;
        if (_attempts is not null)
        {
            attemptId = await _attempts.GetAttemptIdAsync(assignmentId.Value, cancellationToken);
        }

        var access = _tokens.IssueCandidate(user.Id, user.Email!, assignmentId.Value, attemptId);
        _logger.LogInformation("Magic-link consumed for assignment {AssignmentId}", assignmentId.Value);
        return new CandidateTokenResponse(access.Token, access.ExpiresAt, assignmentId.Value);
    }

    public string BuildInviteUrl(string opaqueToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(opaqueToken);
        var origin = (_publicBaseUrl.PublicBaseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(origin))
        {
            throw new InvalidOperationException("PublicBaseUrl is not configured.");
        }

        return $"{origin}/attempt?token={Uri.EscapeDataString(opaqueToken)}";
    }

    private static void EnsureIssuable(AssignmentInviteInfoDto? info)
    {
        if (info is null)
        {
            throw new DomainException(InvalidInviteMessage);
        }

        if (!string.Equals(info.Mode, "async", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(LiveModeMessage);
        }

        if (IsTerminalStatus(info.Status))
        {
            throw new DomainException(NoLongerInvitableMessage);
        }
    }

    private static bool IsInvitable(AssignmentInviteInfoDto info)
        => string.Equals(info.Mode, "async", StringComparison.OrdinalIgnoreCase)
           && !IsTerminalStatus(info.Status);

    private static bool IsTerminalStatus(string? status)
        => status is not null && TerminalStatuses.Contains(status);

    private async Task<ApplicationUser> FindOrCreateCandidateAsync(
        string candidateEmail,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(candidateEmail);
        var existing = await _users.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsDisabled = false,
            CreatedAt = _clock.UtcNow
        };

        var created = await _users.CreateWithoutPasswordAsync(user);
        if (created.Succeeded)
        {
            return user;
        }

        var raced = await _users.FindByEmailAsync(email, cancellationToken);
        if (raced is not null)
        {
            return raced;
        }

        _logger.LogWarning("Candidate user create failed for assignment {AssignmentId}", assignmentId);
        throw new DomainException(InvalidInviteMessage);
    }

    private static string NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new DomainException(InvalidInviteMessage);
        }

        return trimmed.ToLowerInvariant();
    }
}
