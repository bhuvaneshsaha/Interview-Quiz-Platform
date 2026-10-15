namespace InterviewQuiz.Evaluation.Domain;

public sealed class AttemptItemResult
{
    private AttemptItemResult()
    {
    }

    public Guid AttemptId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int SortOrder { get; private set; }
    public string Type { get; private set; } = "";
    public string ScoringMode { get; private set; } = "";
    public int Points { get; private set; }
    public ItemScoreStatus Status { get; private set; }
    public decimal? PointsAwarded { get; private set; }
    public string? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string? ReviewComment { get; private set; }
    public decimal? SuggestedPoints { get; private set; }
    public string? SuggestionFeedback { get; private set; }
    public string? SuggestedByUserId { get; private set; }
    public DateTimeOffset? SuggestedAtUtc { get; private set; }
    public string? SuggestionModel { get; private set; }

    public bool IsHumanReviewable =>
        string.Equals(ScoringMode, "humanOnly", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ScoringMode, "aiAssist", StringComparison.OrdinalIgnoreCase);

    public bool IsAiAssist =>
        string.Equals(ScoringMode, "aiAssist", StringComparison.OrdinalIgnoreCase);

    public void Settle(decimal pointsAwarded, string reviewerUserId, DateTimeOffset reviewedAtUtc, string? comment)
    {
        PointsAwarded = pointsAwarded;
        Status = ItemScoreStatus.Scored;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = reviewedAtUtc;
        ReviewComment = comment;
    }

    public void RecordSuggestion(
        decimal suggestedPoints,
        string? feedback,
        string suggestedByUserId,
        DateTimeOffset suggestedAtUtc,
        string model)
    {
        SuggestedPoints = suggestedPoints;
        SuggestionFeedback = feedback;
        SuggestedByUserId = suggestedByUserId;
        SuggestedAtUtc = suggestedAtUtc;
        SuggestionModel = model;
    }

    public static AttemptItemResult Create(
        Guid attemptId,
        Guid questionId,
        int sortOrder,
        string type,
        string scoringMode,
        int points,
        ItemScoreStatus status,
        decimal? pointsAwarded)
    {
        return new AttemptItemResult
        {
            AttemptId = attemptId,
            QuestionId = questionId,
            SortOrder = sortOrder,
            Type = type,
            ScoringMode = scoringMode,
            Points = points,
            Status = status,
            PointsAwarded = pointsAwarded
        };
    }
}
