using InterviewQuiz.Kernel.Assignments;

namespace InterviewQuiz.Catalog.Application.Services;

internal sealed class EmptyQuizAssignmentUsage : IQuizAssignmentUsage
{
    public static readonly EmptyQuizAssignmentUsage Instance = new();

    public Task<int> CountByQuizAsync(Guid quizId, CancellationToken cancellationToken)
        => Task.FromResult(0);
}
