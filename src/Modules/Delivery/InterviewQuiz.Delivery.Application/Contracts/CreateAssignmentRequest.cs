using System.ComponentModel.DataAnnotations;

namespace InterviewQuiz.Delivery.Application.Contracts;

public sealed class CreateAssignmentRequest
{
    [Required]
    public Guid OpeningId { get; set; }

    [Required]
    public Guid QuizId { get; set; }

    [Required]
    public string CandidateEmail { get; set; } = "";

    [Required]
    public string Mode { get; set; } = "";

    public AssignmentTimingRequest? Timing { get; set; }

    public int? AttemptLimit { get; set; }
}
