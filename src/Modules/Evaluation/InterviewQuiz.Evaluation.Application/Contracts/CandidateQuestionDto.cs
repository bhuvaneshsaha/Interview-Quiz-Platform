using System.Text.Json;
using InterviewQuiz.Catalog.Domain;

namespace InterviewQuiz.Evaluation.Application.Contracts;

/// <summary>
/// Candidate question. Type stays so the client can render controls.
/// Points, scoring mode, and credit mode are omitted (recruiter-only).
/// </summary>
public sealed class CandidateQuestionDto
{
    public Guid Id { get; init; }
    public int SortOrder { get; init; }
    public QuestionType Type { get; init; }
    public string Stem { get; init; } = "";
    public JsonElement Body { get; init; }
    public Guid? SourceQuestionId { get; init; }
}
