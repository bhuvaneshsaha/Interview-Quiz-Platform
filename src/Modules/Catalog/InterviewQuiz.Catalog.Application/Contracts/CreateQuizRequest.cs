using System.ComponentModel.DataAnnotations;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class CreateQuizRequest
{
    [Required]
    public Guid OpeningId { get; set; }

    [Required]
    [MaxLength(Quiz.TitleMaxLength)]
    public string Title { get; set; } = "";

    [MaxLength(Quiz.DescriptionMaxLength)]
    public string? Description { get; set; }

    [Range(0, 80)]
    public int ExpectedExperienceYears { get; set; }

    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<QuestionRequest> Questions { get; set; } = [];
}
