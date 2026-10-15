using System.Diagnostics;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Delivery.Application.Services;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Mail;

namespace InterviewQuiz.Host.Composition;

public sealed class AssignmentInviteCoordinator
{
    private readonly IAssignmentService _assignments;
    private readonly IMagicLinkService _magicLinks;
    private readonly IInviteMailer _mailer;
    private readonly IRevokedInviteAttemptCloser _attemptCloser;
    private readonly ILiveSessionCommands _live;
    private readonly ILogger<AssignmentInviteCoordinator> _logger;

    public AssignmentInviteCoordinator(
        IAssignmentService assignments,
        IMagicLinkService magicLinks,
        IInviteMailer mailer,
        IRevokedInviteAttemptCloser attemptCloser,
        ILiveSessionCommands live,
        ILogger<AssignmentInviteCoordinator> logger)
    {
        _assignments = assignments;
        _magicLinks = magicLinks;
        _mailer = mailer;
        _attemptCloser = attemptCloser;
        _live = live;
        _logger = logger;
    }

    public async Task IssueAndEmailAsync(
        AssignmentResponse assignment,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(assignment.Mode, "async", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var raw = await _magicLinks.IssueAsync(assignment.Id, cancellationToken, expiresAt);
        var url = _magicLinks.BuildInviteUrl(raw);
        assignment.InviteUrl = url;
        await AttachStatusAsync(assignment, cancellationToken);
        await DeliverAsync(assignment, url, assignment.InviteExpiresAt, resend: false, cancellationToken);
    }

    public async Task<InviteResponse> ReissueAsync(
        Guid assignmentId,
        DateTimeOffset? expiresAt,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        await _assignments.EnsureInvitableAsync(assignmentId, cancellationToken);
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken);
        if (string.Equals(assignment.Mode, "live", StringComparison.OrdinalIgnoreCase))
        {
            var raw = await _magicLinks.IssueAsync(assignment.Id, cancellationToken, expiresAt);
            assignment.InviteUrl = _magicLinks.BuildInviteUrl(raw);
            await AttachStatusAsync(assignment, cancellationToken);
            await _live.RecordLinkIssuedAsync(assignment.Id, actorUserId, cancellationToken);
            _logger.LogInformation(
                "Live session {Action} assignment {AssignmentId} attempt {AttemptId} actor {ActorUserId}",
                "linkIssued",
                assignment.Id,
                null,
                actorUserId);
            return ToInvite(assignment);
        }

        await IssueAndEmailAsync(assignment, expiresAt, cancellationToken);
        return ToInvite(assignment);
    }

    public async Task<InviteDeliveryResponse> ResendAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        await _assignments.EnsureInvitableAsync(assignmentId, cancellationToken);
        var raw = await _magicLinks.GetDeliverableTokenAsync(assignmentId, cancellationToken);
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken);
        await AttachStatusAsync(assignment, cancellationToken);
        var url = _magicLinks.BuildInviteUrl(raw);
        await DeliverAsync(assignment, url, assignment.InviteExpiresAt, resend: true, cancellationToken);
        return ToDelivery(assignment);
    }

    public async Task<InviteLifetimeResponse> RevokeAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        await _assignments.EnsureInvitableAsync(assignmentId, cancellationToken);
        try
        {
            await _magicLinks.RevokeAsync(assignmentId, cancellationToken);
        }
        catch (DomainException ex) when (ex.Message == MagicLinkInviteStore.NoInviteToRevokeMessage)
        {
            var existing = await _magicLinks.GetStatusAsync(assignmentId, cancellationToken);
            if (existing.State != MagicLinkInviteStatus.Revoked)
            {
                throw;
            }
        }

        await _attemptCloser.CloseInProgressAsync(assignmentId, cancellationToken);
        var status = await _magicLinks.GetStatusAsync(assignmentId, cancellationToken);
        return new InviteLifetimeResponse(assignmentId, status.State, status.ExpiresAt);
    }

    public async Task<InviteLifetimeResponse> ExtendAsync(
        Guid assignmentId,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        await _assignments.EnsureInvitableAsync(assignmentId, cancellationToken);
        var status = await _magicLinks.ExtendAsync(assignmentId, expiresAt, cancellationToken);
        return new InviteLifetimeResponse(assignmentId, status.State, status.ExpiresAt);
    }

    public async Task AttachStatusAsync(AssignmentResponse assignment, CancellationToken cancellationToken)
    {
        var status = await _magicLinks.GetStatusAsync(assignment.Id, cancellationToken);
        assignment.InviteState = status.State;
        assignment.InviteExpiresAt = status.ExpiresAt;
    }

    private async Task DeliverAsync(
        AssignmentResponse assignment,
        string inviteUrl,
        DateTimeOffset? expiresAt,
        bool resend,
        CancellationToken cancellationToken)
    {
        using var activity = AssignmentService.ActivitySource.StartActivity("assignments.invite.email");
        activity?.SetTag("assignment.id", assignment.Id);

        InviteMailResult result;
        try
        {
            result = await _mailer.SendAsync(
                new InviteMailMessage(assignment.Id, assignment.CandidateEmail, inviteUrl, expiresAt, resend),
                cancellationToken);
        }
        catch (Exception ex)
        {
            var safe = InviteSecretRedactor.Redact(ex.Message);
            _logger.LogWarning(
                "Invite email failed for assignment {AssignmentId}: {Error}",
                assignment.Id,
                safe);
            result = new InviteMailResult(false, string.IsNullOrWhiteSpace(safe) ? "Email could not be sent." : safe);
        }

        var error = result.Succeeded ? null : InviteSecretRedactor.Redact(result.Error);
        if (error is not null && error.Contains(inviteUrl, StringComparison.Ordinal))
        {
            error = "Email could not be sent.";
        }

        var recorded = await _assignments.RecordInviteDeliveryAsync(
            assignment.Id,
            result.Succeeded,
            error,
            cancellationToken);
        assignment.InviteEmailStatus = recorded.InviteEmailStatus;
        assignment.InviteEmailAttemptCount = recorded.InviteEmailAttemptCount;
        assignment.InviteEmailLastAttemptAt = recorded.InviteEmailLastAttemptAt;
        assignment.InviteEmailLastError = recorded.InviteEmailLastError;

        activity?.SetTag("invite.email.status", assignment.InviteEmailStatus);
        _logger.LogInformation(
            "Invite email {Outcome} for assignment {AssignmentId} as {Status}",
            result.Succeeded ? "accepted" : "failed",
            assignment.Id,
            assignment.InviteEmailStatus);
    }

    private static InviteResponse ToInvite(AssignmentResponse assignment)
        => new(
            assignment.Id,
            assignment.InviteUrl ?? "",
            assignment.InviteExpiresAt,
            assignment.InviteEmailStatus,
            assignment.InviteEmailAttemptCount,
            assignment.InviteEmailLastError);

    private static InviteDeliveryResponse ToDelivery(AssignmentResponse assignment)
        => new(
            assignment.Id,
            assignment.InviteEmailStatus,
            assignment.InviteEmailAttemptCount,
            assignment.InviteEmailLastAttemptAt,
            assignment.InviteEmailLastError);
}
