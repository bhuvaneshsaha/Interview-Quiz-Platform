using System.ComponentModel.DataAnnotations;
using InterviewQuiz.Openings.Domain;

namespace InterviewQuiz.Openings.Application.Contracts;

public sealed class CreateOpeningRequest
{
    [Required]
    [MaxLength(Opening.TitleMaxLength)]
    public string Title { get; set; } = "";

    [MaxLength(Opening.JobDescriptionMaxLength)]
    public string JobDescription { get; set; } = "";

    [Required]
    [MaxLength(Opening.OwnerMaxLength)]
    public string Owner { get; set; } = "";

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? ExpectedCloseDate { get; set; }

    [Range(1, 10_000)]
    public int Headcount { get; set; }

    [Range(0, 80)]
    public int ExpectedExperienceYears { get; set; }

    public List<string> Handlers { get; set; } = [];

    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
