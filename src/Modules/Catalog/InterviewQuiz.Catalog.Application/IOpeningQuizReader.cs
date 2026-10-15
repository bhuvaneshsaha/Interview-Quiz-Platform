namespace InterviewQuiz.Catalog.Application;

public sealed record OpeningQuizItem(
    Guid Id,
    string Title,
    int QuestionCount,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Quizzes for one opening. Callers do not read the catalog schema.
/// </summary>
public interface IOpeningQuizReader
{
    Task<IReadOnlyList<OpeningQuizItem>> ListByOpeningAsync(Guid openingId, CancellationToken cancellationToken);
}
