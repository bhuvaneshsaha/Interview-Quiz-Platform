namespace InterviewQuiz.Catalog.Domain;

/// <summary>Per-question scoring: auto, AI-assist (human confirms), or human-only.</summary>
public enum ScoringMode
{
    Auto,
    AiAssist,
    HumanOnly
}
