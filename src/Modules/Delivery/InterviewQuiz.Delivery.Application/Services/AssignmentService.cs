using System.Diagnostics;
using System.Text.Json;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Delivery.Domain;
using InterviewQuiz.Kernel.Assignments;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Delivery.Application.Services;

public sealed class AssignmentService :
    IAssignmentService,
    IAssignmentInviteInfo,
    IAssignmentSnapshotReader,
    IAssignmentLifecycle
{
    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Delivery");

    private readonly IAssignmentRepository _assignments;
    private readonly IOpeningLookup _openings;
    private readonly IQuizSnapshotReader _quizzes;
    private readonly IClock _clock;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(
        IAssignmentRepository assignments,
        IOpeningLookup openings,
        IQuizSnapshotReader quizzes,
        IClock clock,
        ILogger<AssignmentService> logger)
    {
        _assignments = assignments;
        _openings = openings;
        _quizzes = quizzes;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AssignmentResponse> CreateAsync(
        CreateAssignmentRequest request,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("assignments.create");

        var mode = Assignment.ParseMode(request.Mode);
        var opening = await _openings.GetOpeningAsync(request.OpeningId, cancellationToken);
        if (opening is null)
        {
            throw new DomainException(Assignment.OpeningMissingMessage);
        }

        var snapshotDto = await _quizzes.GetSnapshotAsync(request.QuizId, cancellationToken);
        if (snapshotDto is null)
        {
            throw new DomainException(Assignment.QuizMissingMessage);
        }

        if (snapshotDto.OpeningId != request.OpeningId)
        {
            throw new DomainException(Assignment.QuizOpeningMismatchMessage);
        }

        if (snapshotDto.Questions.Count == 0)
        {
            throw new DomainException(Assignment.QuizHasNoQuestionsMessage);
        }

        var assignmentId = Guid.NewGuid();
        var snapshotRowId = Guid.NewGuid();
        var payload = SnapshotJson.FromDto(snapshotDto);
        var snapshot = AssignmentSnapshot.Freeze(
            snapshotRowId,
            assignmentId,
            snapshotDto.Title,
            snapshotDto.Questions.Count,
            payload,
            _clock.UtcNow);

        var assignment = Assignment.Create(
            request.OpeningId,
            request.QuizId,
            request.CandidateEmail,
            mode,
            request.Timing?.OverallDurationMinutes,
            request.AttemptLimit,
            createdByUserId,
            snapshot,
            _clock,
            assignmentId);

        await _assignments.AddAsync(assignment, cancellationToken);
        await _assignments.SaveChangesAsync(cancellationToken);

        activity?.SetTag("assignment.id", assignment.Id);
        activity?.SetTag("opening.id", assignment.OpeningId);
        activity?.SetTag("quiz.id", assignment.QuizId);
        activity?.SetTag("snapshot.id", assignment.SnapshotId);

        _logger.LogInformation(
            "Created assignment {AssignmentId} for opening {OpeningId} quiz {QuizId} snapshot {SnapshotId}",
            assignment.Id,
            assignment.OpeningId,
            assignment.QuizId,
            assignment.SnapshotId);

        return MapResponse(assignment);
    }

    public async Task<AssignmentResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("assignments.get");
        activity?.SetTag("assignment.id", id);

        var assignment = await _assignments.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Assignment), id);

        return MapResponse(assignment);
    }

    public async Task<PagedResult<AssignmentSummaryResponse>> ListAsync(
        AssignmentListQuery query,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("assignments.list");
        var page = new PageRequest(query.Page, query.PageSize);
        var result = await _assignments.ListAsync(query.OpeningId, query.Keyword, page, cancellationToken);
        var items = result.Items.Select(MapSummary).ToList();
        return new PagedResult<AssignmentSummaryResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task EnsureInvitableAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("assignments.invite");
        activity?.SetTag("assignment.id", id);

        var assignment = await _assignments.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Assignment), id);
        assignment.EnsureInvitable();
        _logger.LogInformation("Issuing invite for assignment {AssignmentId}", assignment.Id);
    }

    async Task<AssignmentInviteInfoDto?> IAssignmentInviteInfo.GetAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken);
        if (assignment is null)
        {
            return null;
        }

        return new AssignmentInviteInfoDto(
            assignment.Id,
            assignment.CandidateEmail,
            Camel(assignment.Mode),
            Camel(assignment.Status));
    }

    public async Task<AssignmentWithSnapshotDto?> GetAssignmentWithSnapshotAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken);
        if (assignment is null)
        {
            return null;
        }

        return new AssignmentWithSnapshotDto(
            assignment.Id,
            assignment.OpeningId,
            assignment.QuizId,
            assignment.SnapshotId,
            assignment.CandidateEmail,
            Camel(assignment.Mode),
            assignment.OverallDurationMinutes,
            assignment.AttemptLimit,
            Camel(assignment.Status),
            SnapshotJson.ToDto(assignment.Snapshot.Payload));
    }

    public async Task NotifyAttemptStarted(Guid assignmentId, Guid attemptId, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Assignment), assignmentId);
        assignment.MarkAttemptStarted(_clock);
        await _assignments.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Assignment {AssignmentId} in progress from attempt {AttemptId}",
            assignmentId,
            attemptId);
    }

    public async Task NotifyAttemptSubmitted(
        Guid assignmentId,
        Guid attemptId,
        string resultStatus,
        CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(assignmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Assignment), assignmentId);
        assignment.MarkAttemptSubmitted(resultStatus, _clock);
        await _assignments.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Assignment {AssignmentId} submitted from attempt {AttemptId} as {Status}",
            assignmentId,
            attemptId,
            Camel(assignment.Status));
    }

    internal static AssignmentResponse MapResponse(Assignment assignment)
        => new()
        {
            Id = assignment.Id,
            OpeningId = assignment.OpeningId,
            QuizId = assignment.QuizId,
            SnapshotId = assignment.SnapshotId,
            SnapshotTitle = assignment.Snapshot.Title,
            SnapshotQuestionCount = assignment.Snapshot.QuestionCount,
            CandidateEmail = assignment.CandidateEmail,
            Mode = Camel(assignment.Mode),
            OverallDurationMinutes = assignment.OverallDurationMinutes,
            AttemptLimit = assignment.AttemptLimit,
            Status = Camel(assignment.Status),
            CreatedAtUtc = assignment.CreatedAtUtc,
            UpdatedAtUtc = assignment.UpdatedAtUtc,
            CreatedByUserId = assignment.CreatedByUserId
        };

    internal static AssignmentSummaryResponse MapSummary(Assignment assignment)
        => new()
        {
            Id = assignment.Id,
            OpeningId = assignment.OpeningId,
            QuizId = assignment.QuizId,
            SnapshotId = assignment.SnapshotId,
            SnapshotTitle = assignment.Snapshot.Title,
            SnapshotQuestionCount = assignment.Snapshot.QuestionCount,
            CandidateEmail = assignment.CandidateEmail,
            Mode = Camel(assignment.Mode),
            OverallDurationMinutes = assignment.OverallDurationMinutes,
            AttemptLimit = assignment.AttemptLimit,
            Status = Camel(assignment.Status),
            CreatedAtUtc = assignment.CreatedAtUtc,
            UpdatedAtUtc = assignment.UpdatedAtUtc
        };

    private static string Camel(Enum value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
