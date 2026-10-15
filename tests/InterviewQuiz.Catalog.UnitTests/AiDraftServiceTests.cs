using System.Text.Json;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Application.Services;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Ai;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Catalog.UnitTests;

public sealed class AiDraftServiceTests
{
    private static readonly Guid OpeningId = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001");
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 16, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Create_stores_a_draft_and_does_not_create_a_quiz()
    {
        var quizzes = new FakeQuizRepository();
        var drafts = new FakeAiDraftRepository();
        var service = CreateService(drafts, quizzes);

        var draft = await service.CreateAsync(new CreateAiDraftRequest
        {
            OpeningId = OpeningId,
            ResumeText = "  Ten years of APIs.  ",
            RulesText = "Prefer short stems."
        }, "author-1", CancellationToken.None);

        Assert.Empty(quizzes.Items);
        Assert.Single(drafts.Items);
        Assert.Equal("fake", draft.Model);
        Assert.Equal("generated", draft.Status);
        Assert.Equal("Ten years of APIs.", draft.ResumeText);
        Assert.Equal("Draft: Senior backend engineer", draft.Title);
        Assert.Equal(5, draft.ExpectedExperienceYears);
        Assert.Equal("Draft check for Senior backend engineer.", draft.Questions[0].Stem);
        Assert.Equal("Draft written answer from the resume.", draft.Questions[1].Stem);
        Assert.Equal("Prefer short stems.", draft.Questions[1].Body.GetProperty("guidance").GetString());
        Assert.Null(draft.AcceptedQuizId);
    }

    [Fact]
    public async Task Create_without_a_resume_uses_the_written_answer_stem()
    {
        var service = CreateService(new FakeAiDraftRepository(), new FakeQuizRepository());
        var draft = await service.CreateAsync(new CreateAiDraftRequest { OpeningId = OpeningId }, "author-1", CancellationToken.None);
        Assert.Equal("Draft written answer.", draft.Questions[1].Stem);
        Assert.Equal(FakeAiCompletionClient.ReviewGuidance, draft.Questions[1].Body.GetProperty("guidance").GetString());
    }

    [Fact]
    public async Task Create_includes_a_template_question()
    {
        var templates = new SnapshotTemplates
        {
            Snapshot = new QuizSnapshotDto(
                Guid.NewGuid(),
                Guid.Empty,
                "Template",
                "",
                1,
                new Dictionary<string, string>(),
                [
                    new QuizSnapshotQuestionDto(
                        Guid.NewGuid(),
                        0,
                        QuestionType.TrueFalse,
                        "Template check.",
                        ScoringMode.Auto,
                        null,
                        2,
                        JsonDocument.Parse("""{"correct":false}""").RootElement,
                        null)
                ],
                0,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch)
        };
        var service = CreateService(new FakeAiDraftRepository(), new FakeQuizRepository(), templates);
        var versionId = Guid.NewGuid();
        var draft = await service.CreateAsync(new CreateAiDraftRequest
        {
            OpeningId = OpeningId,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = versionId
        }, "author-1", CancellationToken.None);

        Assert.Equal(3, draft.Questions.Count);
        Assert.Equal("Template check.", draft.Questions[2].Stem);
        Assert.False(draft.Questions[2].Body.GetProperty("correct").GetBoolean());
    }

    [Fact]
    public async Task Create_rejects_a_template_id_without_a_version()
    {
        var drafts = new FakeAiDraftRepository();
        var service = CreateService(drafts, new FakeQuizRepository());
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(new CreateAiDraftRequest
        {
            OpeningId = OpeningId,
            TemplateId = Guid.NewGuid()
        }, "author-1", CancellationToken.None));
        Assert.Equal(AiDraft.TemplatePairMessage, ex.Message);
        Assert.Empty(drafts.Items);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_opening_before_writing()
    {
        var drafts = new FakeAiDraftRepository();
        var service = CreateService(drafts, new FakeQuizRepository(), openings: new FakeOpeningLookup(null));
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(
            new CreateAiDraftRequest { OpeningId = Guid.NewGuid() },
            "author-1",
            CancellationToken.None));
        Assert.Equal("Opening does not exist.", ex.Message);
        Assert.Empty(drafts.Items);
    }

    [Fact]
    public async Task Create_when_ai_is_off_writes_nothing()
    {
        var drafts = new FakeAiDraftRepository();
        var service = CreateService(drafts, new FakeQuizRepository(), completion: DisabledAiCompletionClient.Instance);
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(
            new CreateAiDraftRequest { OpeningId = OpeningId },
            "author-1",
            CancellationToken.None));
        Assert.Equal(AiDraft.UnavailableMessage, ex.Message);
        Assert.Empty(drafts.Items);
    }

    [Fact]
    public async Task Accept_requires_a_question_edit_and_then_creates_one_quiz()
    {
        var quizzes = new FakeQuizRepository();
        var drafts = new FakeAiDraftRepository();
        var service = CreateService(drafts, quizzes);
        var draft = await service.CreateAsync(new CreateAiDraftRequest
        {
            OpeningId = OpeningId,
            ResumeText = "APIs"
        }, "author-1", CancellationToken.None);

        var unchanged = AcceptFrom(draft, draft.Questions[0].Stem);
        var denied = await Assert.ThrowsAsync<DomainException>(() =>
            service.AcceptAsync(draft.Id, unchanged, "author-1", CancellationToken.None));
        Assert.Equal(AiDraft.EditRequiredMessage, denied.Message);
        Assert.Empty(quizzes.Items);

        var edited = AcceptFrom(draft, draft.Questions[0].Stem + " Edited.");
        var accepted = await service.AcceptAsync(draft.Id, edited, "author-1", CancellationToken.None);
        Assert.Single(quizzes.Items);
        Assert.Equal(OpeningId, quizzes.Items[0].OpeningId);
        Assert.Equal(edited.Title, accepted.Title);
        Assert.Contains("Edited.", quizzes.Items[0].Questions[0].Stem);

        var again = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(draft.Id, edited, "author-1", CancellationToken.None));
        Assert.Equal(AiDraft.AlreadyAcceptedMessage, again.Message);
        Assert.Single(quizzes.Items);
    }

    [Fact]
    public async Task Title_only_change_is_not_an_edit()
    {
        var service = CreateService(new FakeAiDraftRepository(), new FakeQuizRepository());
        var draft = await service.CreateAsync(new CreateAiDraftRequest { OpeningId = OpeningId }, "author-1", CancellationToken.None);
        var request = AcceptFrom(draft, draft.Questions[0].Stem);
        request.Title = "A different title";
        var denied = await Assert.ThrowsAsync<DomainException>(() =>
            service.AcceptAsync(draft.Id, request, "author-1", CancellationToken.None));
        Assert.Equal(AiDraft.EditRequiredMessage, denied.Message);
    }

    [Fact]
    public async Task Another_user_cannot_read_the_draft()
    {
        var service = CreateService(new FakeAiDraftRepository(), new FakeQuizRepository());
        var draft = await service.CreateAsync(new CreateAiDraftRequest { OpeningId = OpeningId }, "author-1", CancellationToken.None);
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.GetAsync(draft.Id, "someone-else", CancellationToken.None));
    }

    [Fact]
    public void Fake_draft_stops_at_forty_questions()
    {
        var lines = new StringBuilderLines();
        lines.Append(AiDraftPrompt.OpeningTitlePrefix).Append("Role").Append("\n");
        lines.Append(AiDraftPrompt.ResumePresentPrefix).Append("no").Append("\n");
        lines.Append(AiDraftPrompt.RulesPrefix).Append("none").Append("\n");
        for (var i = 0; i < 50; i++)
        {
            lines.Append(AiDraftPrompt.TemplateQuestionPrefix)
                .Append("""{"type":"trueFalse","stem":"T","scoringMode":"auto","points":1,"body":{"correct":true}}""")
                .Append("\n");
        }

        lines.Append(AiDraftPrompt.ResumeHeader).Append("\n");
        using var document = JsonDocument.Parse(FakeAiCompletionClient.BuildAuthoringDraft(lines.ToString()));
        Assert.Equal(AiDraftPrompt.MaxQuestions, document.RootElement.GetProperty("questions").GetArrayLength());
    }

    private AiDraftService CreateService(
        FakeAiDraftRepository drafts,
        FakeQuizRepository quizzes,
        SnapshotTemplates? templates = null,
        IOpeningLookup? openings = null,
        IAiCompletionClient? completion = null)
    {
        var lookup = openings ?? new FakeOpeningLookup(OpeningId);
        var quizService = new QuizService(
            quizzes,
            lookup,
            new FakeQuestionBankReader(),
            _clock,
            NullLogger<QuizService>.Instance);
        return new AiDraftService(
            drafts,
            lookup,
            templates ?? new SnapshotTemplates(),
            quizService,
            completion ?? new FakeAiCompletionClient(),
            _clock,
            NullLogger<AiDraftService>.Instance);
    }

    private static AcceptAiDraftRequest AcceptFrom(AiDraftResponse draft, string firstStem)
        => new()
        {
            Title = draft.Title,
            Description = "Human description",
            ExpectedExperienceYears = draft.ExpectedExperienceYears,
            Questions = draft.Questions.Select((question, index) => new QuestionRequest
            {
                Type = question.Type,
                Stem = index == 0 ? firstStem : question.Stem,
                ScoringMode = question.ScoringMode,
                CreditMode = question.CreditMode,
                Points = question.Points,
                Body = question.Body.Clone()
            }).ToList()
        };

    private sealed class StringBuilderLines
    {
        private readonly System.Text.StringBuilder _builder = new();
        public StringBuilderLines Append(string value) { _builder.Append(value); return this; }
        public override string ToString() => _builder.ToString();
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class FakeOpeningLookup : IOpeningLookup
    {
        private readonly Guid? _existing;
        public FakeOpeningLookup(Guid? existing) => _existing = existing;

        public Task<OpeningLookupDto?> GetOpeningAsync(Guid id, CancellationToken cancellationToken)
        {
            if (_existing is null || _existing.Value != id)
            {
                return Task.FromResult<OpeningLookupDto?>(null);
            }

            return Task.FromResult<OpeningLookupDto?>(new OpeningLookupDto(
                id,
                "Senior backend engineer",
                "owner",
                new DateOnly(2026, 10, 1),
                null,
                1,
                5));
        }
    }

    private sealed class FakeQuestionBankReader : IQuestionBankReader
    {
        public Task<QuestionBankItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult<QuestionBankItemDto?>(null);
    }

    private sealed class FakeQuizRepository : IQuizRepository
    {
        public List<Quiz> Items { get; } = [];

        public Task<Quiz?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(q => q.Id == id));

        public Task AddAsync(Quiz quiz, CancellationToken cancellationToken)
        {
            Items.Add(quiz);
            return Task.CompletedTask;
        }

        public void SetExpectedRowVersion(Quiz quiz, uint expectedRowVersion)
        {
        }

        public Task<PagedResult<Quiz>> ListAsync(QuizListCriteria criteria, PageRequest page, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<Quiz>(Items, page.Page, page.PageSize, Items.Count));

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeAiDraftRepository : IAiDraftRepository
    {
        public List<AiDraft> Items { get; } = [];

        public Task AddAsync(AiDraft draft, CancellationToken cancellationToken)
        {
            Items.Add(draft);
            return Task.CompletedTask;
        }

        public Task<AiDraft?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SnapshotTemplates : ITemplateService
    {
        public QuizSnapshotDto? Snapshot { get; set; }

        public Task<QuizSnapshotDto> GetVersionSnapshotAsync(Guid templateId, Guid versionId, CancellationToken cancellationToken)
            => Task.FromResult(Snapshot ?? throw new EntityNotFoundException(nameof(TemplateVersion), versionId));

        public Task<PublishTemplateResponse> PublishAsync(Guid quizId, string publishedByUserId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<PagedResult<TemplateSummaryResponse>> ListAsync(TemplateListCriteria criteria, PageRequest page, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<TemplateResponse> GetAsync(Guid id, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<PagedResult<TemplateVersionSummaryResponse>> ListVersionsAsync(Guid templateId, PageRequest page, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<TemplateVersionResponse> GetVersionAsync(Guid templateId, Guid versionId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<TemplateResponse> UpdateMetadataAsync(Guid id, UpdateTemplateMetadataRequest request, string editedByUserId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<TemplateResponse> SaveContentAsync(Guid id, SaveTemplateContentRequest request, string editedByUserId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<QuizResponse> CloneAsync(Guid templateId, Guid versionId, CloneTemplateVersionRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<CandidateQuizPreviewResponse> PreviewVersionAsync(Guid templateId, Guid versionId, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
