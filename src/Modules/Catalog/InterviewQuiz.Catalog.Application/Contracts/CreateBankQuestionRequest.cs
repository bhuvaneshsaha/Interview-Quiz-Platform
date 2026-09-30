using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class CreateBankQuestionRequest
{
    [Required]
    [MaxLength(BankQuestion.TitleMaxLength)]
    public string Title { get; set; } = "";

    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [Range(0, BankQuestion.ExperienceYearsMax)]
    public int ExpectedExperienceYears { get; set; }

    [Required]
    public QuestionType? Type { get; set; }

    [Required]
    [MaxLength(Question.StemMaxLength)]
    public string Stem { get; set; } = "";

    public ScoringMode? ScoringMode { get; set; }

    public CreditMode? CreditMode { get; set; }

    [Range(1, 10_000)]
    public int Points { get; set; } = 1;

    public JsonElement Body { get; set; }
}
