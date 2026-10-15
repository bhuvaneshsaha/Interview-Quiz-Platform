namespace InterviewQuiz.Delivery.Application.Contracts;

public sealed record InviteResponse(
    Guid AssignmentId,
    string InviteUrl,
    DateTimeOffset? ExpiresAt = null,
    string InviteEmailStatus = "none",
    int InviteEmailAttemptCount = 0,
    string? InviteEmailLastError = null);

public sealed record InviteLifetimeResponse(
    Guid AssignmentId,
    string InviteState,
    DateTimeOffset? ExpiresAt);

public sealed record InviteDeliveryResponse(
    Guid AssignmentId,
    string InviteEmailStatus,
    int InviteEmailAttemptCount,
    DateTimeOffset? InviteEmailLastAttemptAt,
    string? InviteEmailLastError);

public sealed class IssueInviteRequest
{
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class ExtendInviteRequest
{
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class CreateAssignmentFromTemplateRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public Guid OpeningId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public Guid TemplateId { get; set; }

    public Guid? TemplateVersionId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string CandidateEmail { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    public string Mode { get; set; } = "";

    public AssignmentTimingRequest? Timing { get; set; }

    public int? AttemptLimit { get; set; }

    public string? QuizTitle { get; set; }

    public DateTimeOffset? InviteExpiresAt { get; set; }
}
