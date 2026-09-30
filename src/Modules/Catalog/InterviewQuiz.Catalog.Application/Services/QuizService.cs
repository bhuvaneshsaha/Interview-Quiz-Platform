using System.Diagnostics;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Catalog.Application.Services;

public sealed class QuizService : IQuizService, IQuizSnapshotReader
{
    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Catalog");

    private readonly IQuizRepository _quizzes;
    private readonly IOpeningLookup _openings;
    private readonly IClock _clock;
    private readonly ILogger<QuizService> _logger;

    public QuizService(
        IQuizRepository quizzes,
        IOpeningLookup openings,
        IClock clock,
        ILogger<QuizService> logger)
    {
        _quizzes = quizzes;
        _openings = openings;
        _clock = clock;
        _logger = logger;
    }

    public async Task<QuizResponse> CreateAsync(CreateQuizRequest request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("quizzes.create");
        await EnsureOpeningExistsAsync(request.OpeningId, cancellationToken);

        var quiz = Quiz.Create(
            request.OpeningId,
            request.Title,
            request.Description,
            request.ExpectedExperienceYears,
            request.Tags,
            MapQuestions(request.Questions),
            _clock);

        await _quizzes.AddAsync(quiz, cancellationToken);
        await _quizzes.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created quiz {QuizId} for opening {OpeningId}", quiz.Id, quiz.OpeningId);
        return Map(quiz);
    }

    public async Task<QuizResponse> UpdateAsync(UpdateQuizRequest request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("quizzes.update");
        var quiz = await _quizzes.GetAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Quiz), request.Id);

        if (quiz.RowVersion != request.RowVersion)
        {
            throw new ConcurrencyException("Quiz was modified by another request. Reload and retry.");
        }

        await EnsureOpeningExistsAsync(request.OpeningId, cancellationToken);

        quiz.Update(
            request.OpeningId,
            request.Title,
            request.Description,
            request.ExpectedExperienceYears,
            request.Tags,
            MapQuestions(request.Questions),
            _clock);

        _quizzes.SetExpectedRowVersion(quiz, request.RowVersion);
        await _quizzes.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated quiz {QuizId}", quiz.Id);
        return Map(quiz);
    }

    public async Task<QuizResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("quizzes.get");
        var quiz = await _quizzes.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Quiz), id);

        return Map(quiz);
    }

    public async Task<QuizSnapshotDto?> GetSnapshotAsync(Guid quizId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("quizzes.snapshot");
        var quiz = await _quizzes.GetAsync(quizId, cancellationToken);
        return quiz is null ? null : MapSnapshot(quiz);
    }

    public async Task<PagedResult<QuizResponse>> ListAsync(
        QuizListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("quizzes.list");
        var result = await _quizzes.ListAsync(criteria, page, cancellationToken);
        var items = result.Items.Select(Map).ToList();
        return new PagedResult<QuizResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    private async Task EnsureOpeningExistsAsync(Guid openingId, CancellationToken cancellationToken)
    {
        var opening = await _openings.GetOpeningAsync(openingId, cancellationToken);
        if (opening is null)
        {
            throw new DomainException("Opening does not exist.");
        }
    }

    private static List<Question> MapQuestions(IReadOnlyList<QuestionRequest> requests)
    {
        var questions = new List<Question>(requests.Count);
        for (var i = 0; i < requests.Count; i++)
        {
            var item = requests[i];
            if (item.Type is null)
            {
                throw new DomainException("Question type is required.");
            }

            questions.Add(Question.Create(
                item.Id,
                i,
                item.Type.Value,
                item.Stem,
                item.ScoringMode,
                item.CreditMode,
                item.Points,
                item.Body,
                item.SourceQuestionId));
        }

        return questions;
    }

    internal static QuizResponse Map(Quiz quiz)
        => new()
        {
            Id = quiz.Id,
            OpeningId = quiz.OpeningId,
            Title = quiz.Title,
            Description = quiz.Description,
            ExpectedExperienceYears = quiz.ExpectedExperienceYears,
            Tags = quiz.Tags,
            Questions = quiz.Questions
                .OrderBy(q => q.SortOrder)
                .ThenBy(q => q.Id)
                .Select(MapQuestion)
                .ToList(),
            OriginTemplateId = quiz.OriginTemplateId,
            SourceTemplateVersionId = quiz.SourceTemplateVersionId,
            RowVersion = quiz.RowVersion,
            CreatedAtUtc = quiz.CreatedAtUtc,
            UpdatedAtUtc = quiz.UpdatedAtUtc
        };

    internal static QuizSnapshotDto MapSnapshot(Quiz quiz)
        => new(
            quiz.Id,
            quiz.OpeningId,
            quiz.Title,
            quiz.Description,
            quiz.ExpectedExperienceYears,
            quiz.Tags,
            quiz.Questions
                .OrderBy(q => q.SortOrder)
                .ThenBy(q => q.Id)
                .Select(q => new QuizSnapshotQuestionDto(
                    q.Id,
                    q.SortOrder,
                    q.Type,
                    q.Stem,
                    q.ScoringMode,
                    q.CreditMode,
                    q.Points,
                    q.Body.RootElement.Clone(),
                    q.SourceQuestionId))
                .ToList(),
            quiz.RowVersion,
            quiz.CreatedAtUtc,
            quiz.UpdatedAtUtc);

    internal static QuestionResponse MapQuestion(Question question)
        => new()
        {
            Id = question.Id,
            SortOrder = question.SortOrder,
            Type = question.Type,
            Stem = question.Stem,
            ScoringMode = question.ScoringMode,
            CreditMode = question.CreditMode,
            Points = question.Points,
            Body = question.Body.RootElement.Clone(),
            SourceQuestionId = question.SourceQuestionId
        };
}
