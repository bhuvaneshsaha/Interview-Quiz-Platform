using System.ComponentModel.DataAnnotations;

namespace InterviewQuiz.Catalog.Application.Contracts;

public sealed class IncludeQuestionsRequest
{
    [Required]
    [MinLength(1)]
    public List<Guid> QuestionIds { get; set; } = [];

    /// <summary>0-based index into the current quiz question list. Null appends. Clamped 0..count.</summary>
    public int? InsertAt { get; set; }

    [Required]
    public uint RowVersion { get; set; }
}
