using System.Diagnostics;
using InterviewQuiz.Catalog.Application.CandidatePreview;
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
        var version = TemplateVersion.FromQuiz(
            template.Id,
            nextNumber,
            quiz,
            publishedByUserId,
            _clock,
            changeNote: "Published from quiz");
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

    public async Task<TemplateResponse> UpdateMetadataAsync(
        Guid id,
        UpdateTemplateMetadataRequest request,
        string editedByUserId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("templates.metadata");
        return await AppendVersionAsync(
            id,
            request.Title,
            request.Description,
            request.ExpectedExperienceYears,
            request.Tags,
            request.RowVersion,
            request.ChangeNote,
            editedByUserId,
            questions: null,
            cancellationToken);
    }

    public async Task<TemplateResponse> SaveContentAsync(
        Guid id,
        SaveTemplateContentRequest request,
        string editedByUserId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("templates.content");
        var mapped = QuizService.MapQuestions(request.Questions ?? []);
        var copies = mapped.Select((question, index) => question.CopyWithNewId(index)).ToList();
        return await AppendVersionAsync(
            id,
            request.Title,
            request.Description,
            request.ExpectedExperienceYears,
            request.Tags,
            request.RowVersion,
            request.ChangeNote,
            editedByUserId,
            copies,
            cancellationToken);
    }

    private async Task<TemplateResponse> AppendVersionAsync(
        Guid id,
        string? title,
        string? description,
        int expectedExperienceYears,
        IReadOnlyDictionary<string, string>? tags,
        uint rowVersion,
        string? changeNote,
        string editedByUserId,
        IReadOnlyList<Question>? questions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(editedByUserId))
        {
            throw new DomainException("Edited-by user is required.");
        }

        if (string.IsNullOrWhiteSpace(changeNote))
        {
            throw new DomainException("Change note is required.");
        }

        var template = await _templates.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Template), id);
        template.EnsureRowVersion(rowVersion);

        var latest = await _templates.GetLatestVersionAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TemplateVersion), id);

        var nextNumber = await _templates.GetMaxVersionNumberAsync(id, cancellationToken) + 1;
        var copies = questions
            ?? latest.Questions
                .OrderBy(q => q.SortOrder)
                .ThenBy(q => q.Id)
                .Select((question, index) => question.CopyWithNewId(index))
                .ToList();

        var version = TemplateVersion.Create(
            template.Id,
            nextNumber,
            title ?? "",
            description ?? "",
            expectedExperienceYears,
            tags,
            latest.PublishedFromQuizId,
            editedByUserId,
            copies,
            _clock,
            changeNote: changeNote);

        template.Touch(_clock);
        await _templates.AddVersionAsync(version, cancellationToken);
        await _templates.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Template {TemplateId} version {VersionNumber} saved by {UserId}",
            template.Id,
            version.VersionNumber,
            editedByUserId);

        return MapDetail(template, version);
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

    public async Task<CandidateQuizPreviewResponse> PreviewVersionAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetVersionSnapshotAsync(templateId, versionId, cancellationToken);
        return CandidatePreviewBuilder.FromSnapshot(snapshot, "Template preview");
    }

    public async Task<QuizSnapshotDto> GetVersionSnapshotAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await _templates.GetVersionAsync(templateId, versionId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TemplateVersion), versionId);

        return new QuizSnapshotDto(
            version.PublishedFromQuizId,
            Guid.Empty,
            version.Title,
            version.Description,
            version.ExpectedExperienceYears,
            version.Tags,
            version.Questions
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
            0,
            version.PublishedAtUtc,
            version.PublishedAtUtc);
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
            UpdatedAtUtc = row.Template.UpdatedAtUtc,
            RowVersion = row.Template.RowVersion
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
            UpdatedAtUtc = template.UpdatedAtUtc,
            RowVersion = template.RowVersion
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
            PublishedByUserId = version.PublishedByUserId,
            ChangeNote = version.ChangeNote,
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
            PublishedByUserId = version.PublishedByUserId,
            ChangeNote = version.ChangeNote,
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
