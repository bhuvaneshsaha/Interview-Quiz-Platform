namespace InterviewQuiz.Delivery.Application.Contracts;

public sealed class AssignmentResponse : AssignmentSummaryResponse
{
    public string CreatedByUserId { get; init; } = "";

    /// <summary>Present only on POST create and invite issue when mode is async.</summary>
    public string? InviteUrl { get; set; }

    /// <summary>none, sent, failed, or resent. Latest email attempt.</summary>
    public string InviteEmailStatus { get; set; } = "none";

    public int InviteEmailAttemptCount { get; set; }

    public DateTimeOffset? InviteEmailLastAttemptAt { get; set; }

    public string? InviteEmailLastError { get; set; }

    /// <summary>none, active, expired, or revoked. Filled by the host from the current magic-link invite.</summary>
    public string InviteState { get; set; } = "none";

    public DateTimeOffset? InviteExpiresAt { get; set; }
}
