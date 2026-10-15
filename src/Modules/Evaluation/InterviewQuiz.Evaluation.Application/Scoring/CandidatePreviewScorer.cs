using System.Text.Json;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Evaluation.Application.Contracts;
using InterviewQuiz.Evaluation.Domain;

namespace InterviewQuiz.Evaluation.Application.Scoring;

public sealed class CandidatePreviewScoreResponse
{
    public decimal AutoPointsAwarded { get; init; }
    public decimal AutoPointsAvailable { get; init; }
    public IReadOnlyList<CandidatePreviewScoreItem> Items { get; init; } = [];
}

public sealed class CandidatePreviewScoreItem
{
    public Guid QuestionId { get; init; }
    public string Status { get; init; } = "";
    public decimal? PointsAwarded { get; init; }
    public int Points { get; init; }
}

public sealed class PreviewScoreRequest
{
    public List<AnswerDto> Answers { get; set; } = [];
}

/// <summary>
/// Scores a preview locally. Nothing is stored and answer keys are not returned.
/// </summary>
public static class CandidatePreviewScorer
{
    public static CandidatePreviewScoreResponse Score(
        QuizSnapshotDto snapshot,
        IReadOnlyList<AnswerDto>? answers)
    {
        var map = new Dictionary<Guid, JsonElement>();
        foreach (var answer in answers ?? [])
        {
            if (answer.QuestionId == Guid.Empty || answer.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                continue;
            }

            map[answer.QuestionId] = answer.Value;
        }

        var scores = AutoScorer.ScoreAttempt(snapshot, map);
        var auto = scores.Where(score => score.Status == ItemScoreStatus.Scored).ToList();
        return new CandidatePreviewScoreResponse
        {
            AutoPointsAwarded = auto.Sum(score => score.PointsAwarded ?? 0m),
            AutoPointsAvailable = auto.Sum(score => score.Points),
            Items = scores.Select(score => new CandidatePreviewScoreItem
            {
                QuestionId = score.QuestionId,
                Status = score.Status == ItemScoreStatus.Scored ? "scored" : "unsettled",
                PointsAwarded = score.PointsAwarded,
                Points = score.Points
            }).ToList()
        };
    }
}
