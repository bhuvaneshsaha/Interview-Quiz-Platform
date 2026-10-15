using System.ComponentModel.DataAnnotations;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class CreateAiDraftRequest
{
    [Required]
    public Guid OpeningId { get; set; }

    [MaxLength(AiDraft.ResumeMaxLength, ErrorMessage = AiDraft.ResumeTooLongMessage)]
    public string? ResumeText { get; set; }

    [MaxLength(AiDraft.RulesMaxLength, ErrorMessage = AiDraft.RulesTooLongMessage)]
    public string? RulesText { get; set; }

    public Guid? TemplateId { get; set; }

    public Guid? TemplateVersionId { get; set; }
}
