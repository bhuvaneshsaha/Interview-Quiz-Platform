using System.Diagnostics;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Catalog.Application.Services;

public sealed class TemplateService : ITemplateService
{
    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Catalog");

    private readonly ITemplateRepository _templates;
    private readonly IQuizRepository _quizzes;
    private readonly IOpeningLookup _openings;
    private readonly IClock _clock;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(
        ITemplateRepository templates,
        IQuizRepository quizzes,
        IOpeningLookup openings,
        IClock clock,
        ILogger<TemplateService> logger)
    {
        _templates = templates;
        _quizzes = quizzes;
        _openings = openings;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PublishTemplateResponse> PublishAsync(
        Guid quizId,
        string publishedByUserId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("templates.publish");
        if (string.IsNullOrWhiteSpace(publishedByUserId))
        {
            throw new DomainException("Published-by user is required.");
        }

        var quiz = await _quizzes.GetAsync(quizId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Quiz), quizId);

        var createdNewTemplate = false;
        Template template;

        if (quiz.OriginTemplateId is { } originId && originId != Guid.Empty)
        {
            template = await _templates.GetAsync(originId, cancellationToken)
                ?? throw new EntityNotFoundException(nameof(Template), originId);
        }
        else
        {
            var existing = await _templates.GetByOriginQuizIdAsync(quiz.Id, cancellationToken);
            if (existing is null)
            {
                template = Template.Create(quiz.Id, _clock);
                await _templates.AddAsync(template, cancellationToken);
                quiz.AttachToTemplate(template.Id, _clock);
                createdNewTemplate = true;
            }
            else
            {
                template = existing;
                quiz.AttachToTemplate(template.Id, _clock);
            }
        }

        var nextNumber = await _templates.GetMaxVersionNumberAsync(template.Id, cancellationToken) + 1;
        var version = TemplateVersion.FromQuiz(template.Id, nextNumber, quiz, publishedByUserId, _clock);
        template.Touch(_clock);
        await _templates.AddVersionAsync(version, cancellationToken);
        await _templates.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Published template {TemplateId} version {VersionNumber} from quiz {QuizId} (createdNewTemplate {CreatedNewTemplate})",
            template.Id,
            version.VersionNumber,
            quiz.Id,
            createdNewTemplate);

        return new PublishTemplateResponse
        {
            TemplateId = template.Id,
            VersionId = version.Id,
            VersionNumber = version.VersionNumber,
            CreatedNewTemplate = createdNewTemplate
        };
    }

    public async Task<PagedResult<TemplateSummaryResponse>> ListAsync(
        TemplateListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("templates.list");
        var result = await _templates.ListLatestAsync(criteria, page, cancellationToken);
        var items = result.Items.Select(MapSummary).ToList();
        return new PagedResult<TemplateSummaryResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<TemplateResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("templates.get");
        var template = await _templates.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Template), id);

        var latest = await _templates.GetLatestVersionAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TemplateVersion), id);

        return MapDetail(template, latest);
    }

    public async Task<PagedResult<TemplateVersionSummaryResponse>> ListVersionsAsync(
        Guid templateId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("templates.versions");
        if (await _templates.GetAsync(templateId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Template), templateId);
        }

        var result = await _templates.ListVersionsAsync(templateId, page, cancellationToken);
        var items = result.Items.Select(MapVersionSummary).ToList();
        return new PagedResult<TemplateVersionSummaryResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<TemplateVersionResponse> GetVersionAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("templates.versions");
        var version = await _templates.GetVersionAsync(templateId, versionId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TemplateVersion), versionId);

        return MapVersion(version);
    }

    public async Task<QuizResponse> CloneAsync(
        Guid templateId,
        Guid versionId,
        CloneTemplateVersionRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("templates.clone");
        var version = await _templates.GetVersionAsync(templateId, versionId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TemplateVersion), versionId);

        await EnsureOpeningExistsAsync(request.OpeningId, cancellationToken);

        var questions = version.Questions
            .OrderBy(q => q.SortOrder)
            .ThenBy(q => q.Id)
            .Select((q, i) => q.CopyWithNewId(i))
            .ToList();

        var title = string.IsNullOrWhiteSpace(request.Title) ? version.Title : request.Title;
        var quiz = Quiz.Create(
            request.OpeningId,
            title,
            version.Description,
            version.ExpectedExperienceYears,
            version.Tags,
            questions,
            _clock,
            originTemplateId: templateId,
            sourceTemplateVersionId: version.Id);

        await _quizzes.AddAsync(quiz, cancellationToken);
        await _quizzes.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cloned template {TemplateId} version {VersionNumber} to quiz {QuizId}",
            templateId,
            version.VersionNumber,
            quiz.Id);

        return QuizService.Map(quiz);
    }

    private async Task EnsureOpeningExistsAsync(Guid openingId, CancellationToken cancellationToken)
    {
        var opening = await _openings.GetOpeningAsync(openingId, cancellationToken);
        if (opening is null)
        {
            throw new DomainException("Opening does not exist.");
        }
    }

    internal static TemplateSummaryResponse MapSummary(TemplateListRow row)
        => new()
        {
            Id = row.Template.Id,
            Title = row.LatestVersion.Title,
            Description = row.LatestVersion.Description,
            ExpectedExperienceYears = row.LatestVersion.ExpectedExperienceYears,
            Tags = row.LatestVersion.Tags,
            LatestVersionId = row.LatestVersion.Id,
            LatestVersionNumber = row.LatestVersion.VersionNumber,
            OriginQuizId = row.Template.OriginQuizId,
            UpdatedAtUtc = row.Template.UpdatedAtUtc
        };

    internal static TemplateResponse MapDetail(Template template, TemplateVersion latest)
        => new()
        {
            Id = template.Id,
            Title = latest.Title,
            Description = latest.Description,
            ExpectedExperienceYears = latest.ExpectedExperienceYears,
            Tags = latest.Tags,
            LatestVersionId = latest.Id,
            LatestVersionNumber = latest.VersionNumber,
            OriginQuizId = template.OriginQuizId,
            CreatedAtUtc = template.CreatedAtUtc,
            UpdatedAtUtc = template.UpdatedAtUtc
        };

    internal static TemplateVersionSummaryResponse MapVersionSummary(TemplateVersion version)
        => new()
        {
            Id = version.Id,
            TemplateId = version.TemplateId,
            VersionNumber = version.VersionNumber,
            Title = version.Title,
            PublishedFromQuizId = version.PublishedFromQuizId,
            PublishedAtUtc = version.PublishedAtUtc,
            QuestionCount = version.Questions.Count
        };

    internal static TemplateVersionResponse MapVersion(TemplateVersion version)
        => new()
        {
            Id = version.Id,
            TemplateId = version.TemplateId,
            VersionNumber = version.VersionNumber,
            Title = version.Title,
            PublishedFromQuizId = version.PublishedFromQuizId,
            PublishedAtUtc = version.PublishedAtUtc,
            QuestionCount = version.Questions.Count,
            Description = version.Description,
            ExpectedExperienceYears = version.ExpectedExperienceYears,
            Tags = version.Tags,
            Questions = version.Questions
                .OrderBy(q => q.SortOrder)
                .ThenBy(q => q.Id)
                .Select(QuizService.MapQuestion)
                .ToList()
        };
}
