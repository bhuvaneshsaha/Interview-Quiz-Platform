using InterviewQuiz.Catalog.Application.Contracts;

namespace InterviewQuiz.Catalog.Application;

/// <summary>
/// In-process snapshot for Delivery to persist at assign time. Other modules must not query catalog tables.
/// </summary>
public interface IQuizSnapshotReader
{
    Task<QuizSnapshotDto?> GetSnapshotAsync(Guid quizId, CancellationToken cancellationToken);
}
