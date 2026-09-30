using System.Text.Json;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Application.Services;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Catalog.UnitTests;

public sealed class TemplateServiceTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero));
    private static readonly Guid OpeningId = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001");
    private static readonly Guid BankItemId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task First_publish_creates_template_version_one_and_sets_origin()
    {
        var quizzes = new FakeQuizRepository();
        var templates = new FakeTemplateRepository();
        var service = CreateService(quizzes, templates);
        var quiz = await CreateQuizWithProvenance(service: null, quizzes);

        var published = await service.PublishAsync(quiz.Id, "author-1", CancellationToken.None);

        Assert.True(published.CreatedNewTemplate);
        Assert.Equal(1, published.VersionNumber);
        var stored = quizzes.Items.Single();
        Assert.Equal(published.TemplateId, stored.OriginTemplateId);
        Assert.Equal(quiz.Id, templates.Templates.Single().OriginQuizId);
        Assert.Equal(1u, stored.RowVersion);
    }

    [Fact]
    public async Task Later_publish_adds_version_on_same_template()
    {
        var quizzes = new FakeQuizRepository();
        var templates = new FakeTemplateRepository();
        var service = CreateService(quizzes, templates);
        var quiz = await CreateQuizWithProvenance(service: null, quizzes);

        var first = await service.PublishAsync(quiz.Id, "author-1", CancellationToken.None);
        var second = await service.PublishAsync(quiz.Id, "author-1", CancellationToken.None);

        Assert.False(second.CreatedNewTemplate);
        Assert.Equal(first.TemplateId, second.TemplateId);
        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(2, templates.Versions.Count);
    }

    [Fact]
    public async Task Recovery_publish_when_origin_missing_but_template_exists()
    {
        var quizzes = new FakeQuizRepository();
        var templates = new FakeTemplateRepository();
        var service = CreateService(quizzes, templates);
        var quiz = await CreateQuizWithProvenance(service: null, quizzes);
        var template = Template.Create(quiz.Id, _clock);
        await templates.AddAsync(template, CancellationToken.None);
        await templates.AddVersionAsync(
            TemplateVersion.FromQuiz(template.Id, 1, quiz, "author-1", _clock),
            CancellationToken.None);

        var published = await service.PublishAsync(quiz.Id, "author-1", CancellationToken.None);

        Assert.False(published.CreatedNewTemplate);
        Assert.Equal(template.Id, published.TemplateId);
        Assert.Equal(2, published.VersionNumber);
        Assert.Equal(template.Id, quizzes.Items.Single().OriginTemplateId);
    }

    [Fact]
    public async Task Clone_then_publish_versions_the_same_template()
    {
        var quizzes = new FakeQuizRepository();
        var templates = new FakeTemplateRepository();
        var service = CreateService(quizzes, templates);
        var quiz = await CreateQuizWithProvenance(service: null, quizzes);
        var first = await service.PublishAsync(quiz.Id, "author-1", CancellationToken.None);

        var cloned = await service.CloneAsync(
            first.TemplateId,
            first.VersionId,
            new CloneTemplateVersionRequest { OpeningId = OpeningId, Title = "Cloned working copy" },
            CancellationToken.None);

        Assert.Equal(first.TemplateId, cloned.OriginTemplateId);
        Assert.Equal(first.VersionId, cloned.SourceTemplateVersionId);

        var second = await service.PublishAsync(cloned.Id, "author-1", CancellationToken.None);

        Assert.False(second.CreatedNewTemplate);
        Assert.Equal(first.TemplateId, second.TemplateId);
        Assert.Equal(2, second.VersionNumber);
    }

    [Fact]
    public async Task Publish_and_clone_copy_new_question_ids_and_preserve_sourceQuestionId()
    {
        var quizzes = new FakeQuizRepository();
        var templates = new FakeTemplateRepository();
        var service = CreateService(quizzes, templates);
        var quiz = await CreateQuizWithProvenance(service: null, quizzes);
        var originalQuestionId = quiz.Questions.Single().Id;

        var published = await service.PublishAsync(quiz.Id, "author-1", CancellationToken.None);
        var version = templates.Versions.Single(v => v.Id == published.VersionId);
        var publishedQuestion = version.Questions.Single();

        Assert.NotEqual(originalQuestionId, publishedQuestion.Id);
        Assert.Equal(BankItemId, publishedQuestion.SourceQuestionId);

        var cloned = await service.CloneAsync(
            published.TemplateId,
            published.VersionId,
            new CloneTemplateVersionRequest { OpeningId = OpeningId },
            CancellationToken.None);

        Assert.NotEqual(publishedQuestion.Id, cloned.Questions.Single().Id);
        Assert.Equal(BankItemId, cloned.Questions.Single().SourceQuestionId);
        Assert.Equal(version.Title, cloned.Title);
    }

    [Fact]
    public async Task Clone_unknown_opening_is_rejected()
    {
        var quizzes = new FakeQuizRepository();
        var templates = new FakeTemplateRepository();
        var service = CreateService(quizzes, templates, openingExists: false);
        var quiz = await CreateQuizWithProvenance(service: null, quizzes);
        var published = await service.PublishAsync(quiz.Id, "author-1", CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CloneAsync(
                published.TemplateId,
                published.VersionId,
                new CloneTemplateVersionRequest { OpeningId = Guid.NewGuid() },
                CancellationToken.None));

        Assert.Equal("Opening does not exist.", ex.Message);
    }

    private TemplateService CreateService(
        FakeQuizRepository quizzes,
        FakeTemplateRepository templates,
        bool openingExists = true)
        => new(
            templates,
            quizzes,
            new FakeOpeningLookup(openingExists ? OpeningId : null),
            _clock,
            NullLogger<TemplateService>.Instance);

    private async Task<Quiz> CreateQuizWithProvenance(TemplateService? service, FakeQuizRepository quizzes)
    {
        _ = service;
        var quizService = new QuizService(
            quizzes,
            new FakeOpeningLookup(OpeningId),
            new FakeQuestionBankReader(),
            _clock,
            NullLogger<QuizService>.Instance);

        var created = await quizService.CreateAsync(
            new CreateQuizRequest
            {
                OpeningId = OpeningId,
                Title = "Backend working copy",
                Description = "Authoring sample",
                ExpectedExperienceYears = 5,
                Tags = new Dictionary<string, string> { ["Role"] = "Backend" },
                Questions =
                [
                    new QuestionRequest
                    {
                        Type = QuestionType.TrueFalse,
                        Stem = "A quiz belongs to one opening.",
                        ScoringMode = ScoringMode.Auto,
                        Points = 1,
                        Body = JsonDocument.Parse("""{ "correct": true }""").RootElement,
                        SourceQuestionId = BankItemId
                    }
                ]
            },
            CancellationToken.None);

        return quizzes.Items.Single(q => q.Id == created.Id);
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
                "Opening",
                "owner@example.com",
                new DateOnly(2026, 10, 1),
                null,
                1,
                5));
        }
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

        public Task<PagedResult<Quiz>> ListAsync(
            QuizListCriteria criteria,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            var materialized = Items.ToList();
            var slice = materialized.Skip(page.Skip).Take(page.PageSize).ToList();
            return Task.FromResult(new PagedResult<Quiz>(slice, page.Page, page.PageSize, materialized.Count));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTemplateRepository : ITemplateRepository
    {
        public List<Template> Templates { get; } = [];
        public List<TemplateVersion> Versions { get; } = [];

        public Task<Template?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Templates.FirstOrDefault(t => t.Id == id));

        public Task<Template?> GetByOriginQuizIdAsync(Guid quizId, CancellationToken cancellationToken)
            => Task.FromResult(Templates.FirstOrDefault(t => t.OriginQuizId == quizId));

        public Task<TemplateVersion?> GetVersionAsync(Guid templateId, Guid versionId, CancellationToken cancellationToken)
            => Task.FromResult(Versions.FirstOrDefault(v => v.TemplateId == templateId && v.Id == versionId));

        public Task<TemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken cancellationToken)
            => Task.FromResult(
                Versions.Where(v => v.TemplateId == templateId)
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefault());

        public Task<int> GetMaxVersionNumberAsync(Guid templateId, CancellationToken cancellationToken)
            => Task.FromResult(
                Versions.Where(v => v.TemplateId == templateId)
                    .Select(v => v.VersionNumber)
                    .DefaultIfEmpty(0)
                    .Max());

        public Task AddAsync(Template template, CancellationToken cancellationToken)
        {
            Templates.Add(template);
            return Task.CompletedTask;
        }

        public Task AddVersionAsync(TemplateVersion version, CancellationToken cancellationToken)
        {
            Versions.Add(version);
            return Task.CompletedTask;
        }

        public Task<PagedResult<TemplateListRow>> ListLatestAsync(
            TemplateListCriteria criteria,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            var rows = Templates
                .Select(t => new TemplateListRow(
                    t,
                    Versions.Where(v => v.TemplateId == t.Id).OrderByDescending(v => v.VersionNumber).First()))
                .ToList();
            return Task.FromResult(new PagedResult<TemplateListRow>(rows, page.Page, page.PageSize, rows.Count));
        }

        public Task<PagedResult<TemplateVersion>> ListVersionsAsync(
            Guid templateId,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            var items = Versions
                .Where(v => v.TemplateId == templateId)
                .OrderByDescending(v => v.VersionNumber)
                .ToList();
            return Task.FromResult(new PagedResult<TemplateVersion>(items, page.Page, page.PageSize, items.Count));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeQuestionBankReader : IQuestionBankReader
    {
        public Task<QuestionBankItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult<QuestionBankItemDto?>(null);
    }
}
