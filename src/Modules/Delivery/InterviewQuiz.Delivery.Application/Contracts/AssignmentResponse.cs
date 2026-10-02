namespace InterviewQuiz.Delivery.Application.Contracts;

public sealed class AssignmentResponse : AssignmentSummaryResponse
{
    public string CreatedByUserId { get; init; } = "";

    /// <summary>Present only on POST create when mode is async.</summary>
    public string? InviteUrl { get; set; }
}
