using System.Diagnostics;
using System.Text.Json;
using InterviewQuiz.Catalog.Application.Ai;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Ai;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Openings.Application;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Catalog.Application.Services;

public sealed class AiDraftService : IAiDraftService
{
    private readonly IAiDraftRepository _drafts;
    private readonly IOpeningLookup _openings;
    private readonly ITemplateService _templates;
    private readonly IQuizService _quizzes;
    private readonly IAiCompletionClient _completion;
    private readonly IClock _clock;
    private readonly ILogger<AiDraftService> _logger;

    public AiDraftService(
        IAiDraftRepository drafts,
        IOpeningLookup openings,
        ITemplateService templates,
        IQuizService quizzes,
        IAiCompletionClient completion,
        IClock clock,
        ILogger<AiDraftService> logger)
    {
        _drafts = drafts;
        _openings = openings;
        _templates = templates;
        _quizzes = quizzes;
        _completion = completion;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AiDraftResponse> CreateAsync(
        CreateAiDraftRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        using var activity = QuizService.ActivitySource.StartActivity("ai.draft.create");
        var resume = AiDraftPromptBuilder.NormalizeOptional(
            request.ResumeText,
            AiDraft.ResumeMaxLength,
            AiDraft.ResumeTooLongMessage);
        var rules = AiDraftPromptBuilder.NormalizeOptional(
            request.RulesText,
            AiDraft.RulesMaxLength,
            AiDraft.RulesTooLongMessage);
        if (rules is not null)
        {
            rules = AiDraftPromptBuilder.CollapseRules(rules);
        }

        var opening = await _openings.GetOpeningAsync(request.OpeningId, cancellationToken)
            ?? throw new DomainException("Opening does not exist.");

        var templateId = EmptyToNull(request.TemplateId);
        var templateVersionId = EmptyToNull(request.TemplateVersionId);
        if (templateId is null != templateVersionId is null)
        {
            throw new DomainException(AiDraft.TemplatePairMessage);
        }

        var templateQuestions = templateId is null
            ? []
            : (await _templates.GetVersionSnapshotAsync(templateId.Value, templateVersionId!.Value, cancellationToken)).Questions;

        if (!_completion.IsEnabled)
        {
            throw new DomainException(AiDraft.UnavailableMessage);
        }

        var prompt = AiDraftPromptBuilder.Build(opening, resume, rules, templateQuestions);
        string content;
        try
        {
            content = await _completion.CompleteAsync(AiDraftPrompt.SystemPrompt, prompt, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DomainException)
        {
            throw new DomainException(AiDraft.FailedMessage);
        }

        var parsed = AiDraftContent.Read(content);
        var title = TrimTitle(string.IsNullOrWhiteSpace(parsed.Title) ? "Draft: " + opening.Title : parsed.Title);
        var years = Math.Clamp(opening.ExpectedExperienceYears, 0, 80);
        var draft = AiDraft.Create(
            opening.Id,
            title,
            years,
            resume,
            rules,
            templateId,
            templateVersionId,
            parsed.CanonicalJson,
            _completion.ModelName,
            userId,
            _clock.UtcNow);

        await _drafts.AddAsync(draft, cancellationToken);
        await _drafts.SaveChangesAsync(cancellationToken);

        activity?.SetTag("ai.draft.id", draft.Id);
        activity?.SetTag("ai.model", draft.Model);
        _logger.LogInformation(
            "Created AI draft {DraftId} for opening {OpeningId} with model {Model}",
            draft.Id,
            draft.OpeningId,
            draft.Model);
        return Map(draft, parsed.Questions);
    }

    public async Task<AiDraftResponse> GetAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        using var activity = QuizService.ActivitySource.StartActivity("ai.draft.get");
        var draft = await RequireOwnAsync(id, userId, cancellationToken);
        var questions = QuestionsFromCanonical(draft.GeneratedQuestionsJson);
        return Map(draft, questions);
    }

    public async Task<AiDraftAcceptResponse> AcceptAsync(
        Guid id,
        AcceptAiDraftRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        using var activity = QuizService.ActivitySource.StartActivity("ai.draft.accept");
        var draft = await RequireOwnAsync(id, userId, cancellationToken);
        if (draft.Status == AiDraft.StatusAccepted)
        {
            throw new ConflictException(AiDraft.AlreadyAcceptedMessage);
        }

        var incoming = QuizService.MapQuestions(request.Questions);
        var incomingCanonical = AiDraftContent.Canonical(incoming);
        if (AiDraftContent.SameQuestions(draft.GeneratedQuestionsJson, incomingCanonical))
        {
            throw new DomainException(AiDraft.EditRequiredMessage);
        }

        var quiz = await _quizzes.CreateAsync(new CreateQuizRequest
        {
            OpeningId = draft.OpeningId,
            Title = request.Title,
            Description = request.Description,
            ExpectedExperienceYears = request.ExpectedExperienceYears,
            Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Questions = request.Questions
        }, cancellationToken);

        draft.MarkAccepted(quiz.Id, userId, _clock.UtcNow);
        await _drafts.SaveChangesAsync(cancellationToken);

        activity?.SetTag("ai.draft.id", draft.Id);
        activity?.SetTag("quiz.id", quiz.Id);
        _logger.LogInformation(
            "Accepted AI draft {DraftId} as quiz {QuizId}",
            draft.Id,
            quiz.Id);
        return new AiDraftAcceptResponse
        {
            DraftId = draft.Id,
            QuizId = quiz.Id,
            Title = quiz.Title
        };
    }

    private async Task<AiDraft> RequireOwnAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var draft = await _drafts.GetAsync(id, cancellationToken);
        if (draft is null || !string.Equals(draft.CreatedByUserId, userId, StringComparison.Ordinal))
        {
            throw new EntityNotFoundException(nameof(AiDraft), id);
        }

        return draft;
    }

    private static List<Question> QuestionsFromCanonical(string canonical)
    {
        try
        {
            using var document = JsonDocument.Parse(canonical);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new DomainException(AiDraft.FailedMessage);
            }

            var questions = new List<Question>();
            var index = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var type = JsonSerializer.Deserialize<QuestionType>(
                    item.GetProperty("type").GetRawText(),
                    CatalogJson.SerializerOptions);
                var stem = item.GetProperty("stem").GetString() ?? "";
                var scoring = JsonSerializer.Deserialize<ScoringMode>(
                    item.GetProperty("scoringMode").GetRawText(),
                    CatalogJson.SerializerOptions);
                CreditMode? credit = null;
                if (item.TryGetProperty("creditMode", out var creditElement)
                    && creditElement.ValueKind == JsonValueKind.String)
                {
                    credit = JsonSerializer.Deserialize<CreditMode>(creditElement.GetRawText(), CatalogJson.SerializerOptions);
                }

                var points = item.GetProperty("points").GetInt32();
                var body = item.GetProperty("body");
                questions.Add(Question.Create(null, index, type, stem, scoring, credit, points, body));
                index++;
            }

            return questions;
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new DomainException(AiDraft.FailedMessage);
        }
    }

    private static AiDraftResponse Map(AiDraft draft, IReadOnlyList<Question> questions)
        => new()
        {
            Id = draft.Id,
            OpeningId = draft.OpeningId,
            Title = draft.Title,
            ExpectedExperienceYears = draft.ExpectedExperienceYears,
            ResumeText = draft.ResumeText,
            RulesText = draft.RulesText,
            TemplateId = draft.TemplateId,
            TemplateVersionId = draft.TemplateVersionId,
            Model = draft.Model,
            Status = draft.Status,
            Questions = questions.Select(QuizService.MapQuestion).ToList(),
            AcceptedQuizId = draft.AcceptedQuizId,
            CreatedAtUtc = draft.CreatedAtUtc
        };

    private static string TrimTitle(string title)
    {
        var trimmed = title.Trim();
        if (trimmed.Length > Quiz.TitleMaxLength)
        {
            trimmed = trimmed[..Quiz.TitleMaxLength].Trim();
        }

        return trimmed.Length == 0 ? "Draft" : trimmed;
    }

    private static Guid? EmptyToNull(Guid? id)
        => id is null || id == Guid.Empty ? null : id;
}
