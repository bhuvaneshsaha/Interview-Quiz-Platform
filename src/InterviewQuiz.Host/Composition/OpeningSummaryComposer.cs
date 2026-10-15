using InterviewQuiz.Access.Application;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Kernel.Permissions;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Contracts;

namespace InterviewQuiz.Host.Composition;

/// <summary>
/// Opening quizzes and candidate counts, composed through module public APIs.
/// </summary>
public sealed class OpeningSummaryComposer
{
    private readonly IOpeningService _openings;
    private readonly IOpeningQuizReader _quizzes;
    private readonly IOpeningAssignmentReader _assignments;
    private readonly IAssignmentAttemptReader _attempts;
    private readonly ICurrentUserQuery _currentUser;

    public OpeningSummaryComposer(
        IOpeningService openings,
        IOpeningQuizReader quizzes,
        IOpeningAssignmentReader assignments,
        IAssignmentAttemptReader attempts,
        ICurrentUserQuery currentUser)
    {
        _openings = openings;
        _quizzes = quizzes;
        _assignments = assignments;
        _attempts = attempts;
        _currentUser = currentUser;
    }

    public async Task<OpeningSummaryResponse> GetAsync(
        Guid openingId,
        System.Security.Claims.ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        _ = await _openings.GetAsync(openingId, cancellationToken);

        var permissions = _currentUser.GetPermissions(user);
        var canReadQuizzes = permissions.Contains(PermissionCodes.Catalog.QuizzesRead, StringComparer.Ordinal);
        var canReadAssignments = permissions.Contains(PermissionCodes.Delivery.AssignmentsRead, StringComparer.Ordinal);

        if (!canReadQuizzes && !canReadAssignments)
        {
            return new OpeningSummaryResponse { OpeningId = openingId };
        }

        var quizzes = canReadQuizzes
            ? await _quizzes.ListByOpeningAsync(openingId, cancellationToken)
            : [];
        var assignments = canReadAssignments
            ? await _assignments.ListByOpeningAsync(openingId, cancellationToken)
            : [];
        var activity = canReadAssignments
            ? await _attempts.ListForAssignmentsAsync(
                assignments.Select(item => item.AssignmentId).ToArray(),
                cancellationToken)
            : [];
        var attempts = activity.ToDictionary(
            item => item.AssignmentId,
            item => new OpeningSummaryMath.AttemptRow(item.Started, item.Submitted));

        OpeningCandidateCounts? totals = null;
        if (canReadAssignments)
        {
            totals = Map(OpeningSummaryMath.Count(
                assignments.Select(item => new OpeningSummaryMath.AssignmentRow(
                    item.AssignmentId,
                    item.QuizId,
                    item.CandidateEmail)),
                attempts));
        }

        IReadOnlyList<OpeningQuizSummary>? quizSummaries = null;
        if (canReadQuizzes)
        {
            quizSummaries = quizzes.Select(quiz =>
            {
                OpeningCandidateCounts? quizCounts = null;
                if (canReadAssignments)
                {
                    quizCounts = Map(OpeningSummaryMath.Count(
                        assignments
                            .Where(item => item.QuizId == quiz.Id)
                            .Select(item => new OpeningSummaryMath.AssignmentRow(
                                item.AssignmentId,
                                item.QuizId,
                                item.CandidateEmail)),
                        attempts));
                }

                return new OpeningQuizSummary
                {
                    Id = quiz.Id,
                    Title = quiz.Title,
                    QuestionCount = quiz.QuestionCount,
                    UpdatedAtUtc = quiz.UpdatedAtUtc,
                    Candidates = quizCounts
                };
            }).ToArray();
        }

        return new OpeningSummaryResponse
        {
            OpeningId = openingId,
            Quizzes = quizSummaries,
            Candidates = totals
        };
    }

    private static OpeningCandidateCounts Map(OpeningSummaryMath.Counts counts)
        => new()
        {
            Assigned = counts.Assigned,
            Appeared = counts.Appeared,
            Submitted = counts.Submitted
        };
}
