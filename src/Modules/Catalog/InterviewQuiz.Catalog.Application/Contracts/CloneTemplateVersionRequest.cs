using System.ComponentModel.DataAnnotations;

namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class CloneTemplateVersionRequest
{
    [Required]
    public Guid OpeningId { get; set; }

    [MaxLength(Domain.Quiz.TitleMaxLength)]
    public string? Title { get; set; }
}
