using System.Diagnostics;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Catalog.Application.Services;

public sealed class BankQuestionService : IBankQuestionService
{
    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Catalog");

    private readonly IBankQuestionRepository _bank;
    private readonly IClock _clock;
    private readonly ILogger<BankQuestionService> _logger;

    public BankQuestionService(
        IBankQuestionRepository bank,
        IClock clock,
        ILogger<BankQuestionService> logger)
    {
        _bank = bank;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BankQuestionResponse> CreateAsync(
        CreateBankQuestionRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("questions.write");
        var question = BankQuestion.Create(
            request.Title,
            request.Tags,
            request.ExpectedExperienceYears,
            RequireType(request.Type),
            request.Stem,
            request.ScoringMode,
            request.CreditMode,
            request.Points,
            request.Body,
            _clock);

        await _bank.AddAsync(question, cancellationToken);
        await _bank.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created bank question {QuestionId}", question.Id);
        return Map(question);
    }

    public async Task<BankQuestionResponse> UpdateAsync(
        Guid id,
        UpdateBankQuestionRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("questions.write");
        var question = await _bank.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(BankQuestion), id);

        if (question.RowVersion != request.RowVersion)
        {
            throw new ConcurrencyException("Question was modified by another request. Reload and retry.");
        }

        question.Update(
            request.Title,
            request.Tags,
            request.ExpectedExperienceYears,
            RequireType(request.Type),
            request.Stem,
            request.ScoringMode,
            request.CreditMode,
            request.Points,
            request.Body,
            _clock);

        _bank.SetExpectedRowVersion(question, request.RowVersion);
        await _bank.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated bank question {QuestionId}", question.Id);
        return Map(question);
    }

    public async Task<BankQuestionResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("questions.get");
        var question = await _bank.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(BankQuestion), id);

        return Map(question);
    }

    public async Task<PagedResult<BankQuestionResponse>> ListAsync(
        BankQuestionListCriteria criteria,
        PageRequest page,
        bool canListArchived,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("questions.list");
        if (criteria.Archived && !canListArchived)
        {
            throw new ForbiddenException("Listing archived questions requires questions.write.");
        }

        var result = await _bank.ListAsync(criteria, page, cancellationToken);
        var items = result.Items.Select(Map).ToList();
        return new PagedResult<BankQuestionResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<BankQuestionResponse> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("questions.archive");
        var question = await _bank.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(BankQuestion), id);

        question.Archive(_clock);
        await _bank.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Archived bank question {QuestionId}", question.Id);
        return Map(question);
    }

    public async Task<BankQuestionResponse> UnarchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("questions.archive");
        var question = await _bank.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(BankQuestion), id);

        question.Unarchive(_clock);
        await _bank.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unarchived bank question {QuestionId}", question.Id);
        return Map(question);
    }

    private static QuestionType RequireType(QuestionType? type)
    {
        if (type is null)
        {
            throw new DomainException("Question type is required.");
        }

        return type.Value;
    }

    internal static BankQuestionResponse Map(BankQuestion question)
        => new()
        {
            Id = question.Id,
            Title = question.Title,
            Tags = question.Tags,
            ExpectedExperienceYears = question.ExpectedExperienceYears,
            Type = question.Type,
            Stem = question.Stem,
            ScoringMode = question.ScoringMode,
            CreditMode = question.CreditMode,
            Points = question.Points,
            Body = question.Body.RootElement.Clone(),
            ArchivedAtUtc = question.ArchivedAtUtc,
            RowVersion = question.RowVersion,
            CreatedAtUtc = question.CreatedAtUtc,
            UpdatedAtUtc = question.UpdatedAtUtc
        };
}
