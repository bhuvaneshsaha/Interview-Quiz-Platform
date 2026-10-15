using System.Text.Json;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Application.Services;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Users;
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

    [Fact]
    public async Task Metadata_edit_creates_the_next_version_and_leaves_the_previous_questions_unchanged()
    {
        var quizzes = new FakeQuizRepository();
        var templates = new FakeTemplateRepository();
        var service = CreateService(quizzes, templates);
        var quiz = await CreateQuizWithProvenance(service: null, quizzes);
        var published = await service.PublishAsync(quiz.Id, "author-1", CancellationToken.None);
        var versionOne = templates.Versions.Single(version => version.Id == published.VersionId);
        var originalQuestionId = versionOne.Questions.Single().Id;
        var rowVersion = templates.Templates.Single().RowVersion;

        var updated = await service.UpdateMetadataAsync(
            published.TemplateId,
            new UpdateTemplateMetadataRequest
            {
                Title = "Renamed template",
                Description = "Title fix",
                ExpectedExperienceYears = 6,
                Tags = new Dictionary<string, string> { ["Role"] = "Backend" },
                RowVersion = rowVersion,
                ChangeNote = "Rename only"
            },
            "author-2",
            CancellationToken.None);

        Assert.Equal(2, updated.LatestVersionNumber);
        Assert.Equal("Renamed template", updated.Title);
        Assert.Equal(rowVersion + 1, updated.RowVersion);
        Assert.Equal(originalQuestionId, versionOne.Questions.Single().Id);

        var versionTwo = templates.Versions.Single(version => version.VersionNumber == 2);
        Assert.Equal("Rename only", versionTwo.ChangeNote);
        Assert.Equal("author-2", versionTwo.PublishedByUserId);
        Assert.Empty(versionTwo.Questions);
        Assert.Equal(versionOne.Id, versionTwo.ContentVersionId);
        Assert.Equal(versionOne.PublishedFromQuizId, versionTwo.PublishedFromQuizId);

        var loaded = await service.GetVersionAsync(
            published.TemplateId,
            versionTwo.Id,
            CancellationToken.None);
        Assert.Equal(originalQuestionId, loaded.Questions.Single().Id);
        Assert.Equal(BankItemId, loaded.Questions.Single().SourceQuestionId);
        Assert.Equal("Ada Author", loaded.PublishedByDisplayName);

        var again = await service.UpdateMetadataAsync(
            published.TemplateId,
            new UpdateTemplateMetadataRequest
            {
                Title = "Renamed again",
                Description = "Still the same questions",
                ExpectedExperienceYears = 6,
                Tags = new Dictionary<string, string> { ["Role"] = "Backend" },
                RowVersion = updated.RowVersion,
                ChangeNote = "Second rename"
            },
            "author-2",
            CancellationToken.None);
        Assert.Equal(3, again.LatestVersionNumber);
        var versionThree = templates.Versions.Single(version => version.VersionNumber == 3);
        Assert.Empty(versionThree.Questions);
        Assert.Equal(versionOne.Id, versionThree.ContentVersionId);
        Assert.Equal(originalQuestionId, versionOne.Questions.Single().Id);
    }

    [Fact]
    public async Task Content_edit_copies_questions_with_new_ids_and_rejects_a_stale_row_version()
    {
        var quizzes = new FakeQuizRepository();
        var templates = new FakeTemplateRepository();
        var service = CreateService(quizzes, templates);
        var quiz = await CreateQuizWithProvenance(service: null, quizzes);
        var published = await service.PublishAsync(quiz.Id, "author-1", CancellationToken.None);
        var rowVersion = templates.Templates.Single().RowVersion;

        var stale = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.SaveContentAsync(
                published.TemplateId,
                new SaveTemplateContentRequest
                {
                    Title = "Backend working copy",
                    Description = "Authoring sample",
                    ExpectedExperienceYears = 5,
                    RowVersion = rowVersion + 9,
                    ChangeNote = "Should fail",
                    Questions = []
                },
                "author-1",
                CancellationToken.None));
        Assert.Contains("Reload and retry", stale.Message, StringComparison.Ordinal);

        var emptyNote = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateMetadataAsync(
                published.TemplateId,
                new UpdateTemplateMetadataRequest
                {
                    Title = "Backend working copy",
                    ExpectedExperienceYears = 5,
                    RowVersion = rowVersion,
                    ChangeNote = "  "
                },
                "author-1",
                CancellationToken.None));
        Assert.Equal("Change note is required.", emptyNote.Message);

        var saved = await service.SaveContentAsync(
            published.TemplateId,
            new SaveTemplateContentRequest
            {
                Title = "Edited content",
                Description = "Authoring sample",
                ExpectedExperienceYears = 5,
                Tags = new Dictionary<string, string> { ["Role"] = "Backend" },
                RowVersion = rowVersion,
                ChangeNote = "Tighten the stem",
                Questions =
                [
                    new QuestionRequest
                    {
                        Type = QuestionType.TrueFalse,
                        Stem = "A template edit copies questions.",
                        ScoringMode = ScoringMode.Auto,
                        Points = 2,
                        Body = JsonDocument.Parse("""{ "correct": true }""").RootElement,
                        SourceQuestionId = BankItemId
                    }
                ]
            },
            "author-1",
            CancellationToken.None);

        Assert.Equal(2, saved.LatestVersionNumber);
        var versionTwo = templates.Versions.Single(version => version.VersionNumber == 2);
        Assert.Equal("A template edit copies questions.", versionTwo.Questions.Single().Stem);
        Assert.Equal(BankItemId, versionTwo.Questions.Single().SourceQuestionId);
        Assert.NotEqual(
            templates.Versions.Single(version => version.VersionNumber == 1).Questions.Single().Id,
            versionTwo.Questions.Single().Id);
    }

    private TemplateService CreateService(
        FakeQuizRepository quizzes,
        FakeTemplateRepository templates,
        bool openingExists = true)
        => new(
            templates,
            quizzes,
            new FakeOpeningLookup(openingExists ? OpeningId : null),
            new FakeUsers(),
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
        {
            var version = Versions.FirstOrDefault(v => v.TemplateId == templateId && v.Id == versionId);
            Hydrate(version);
            return Task.FromResult(version);
        }

        public Task<TemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken cancellationToken)
        {
            var version = Versions.Where(v => v.TemplateId == templateId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();
            Hydrate(version);
            return Task.FromResult(version);
        }

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
            foreach (var version in items)
            {
                Hydrate(version);
            }

            return Task.FromResult(new PagedResult<TemplateVersion>(items, page.Page, page.PageSize, items.Count));
        }

        private void Hydrate(TemplateVersion? version)
        {
            if (version?.ContentVersionId is not { } contentId)
            {
                return;
            }

            var owner = Versions.Single(item => item.Id == contentId);
            version.UseSharedQuestions(owner.ReadQuestions());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeUsers : IAssignableUserLookup
    {
        public Task<IReadOnlyList<AssignableUserDto>> SearchAsync(
            string? query,
            int limit,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AssignableUserDto>>([]);

        public Task<IReadOnlyDictionary<string, AssignableUserDto>> GetByIdsAsync(
            IReadOnlyCollection<string> userIds,
            CancellationToken cancellationToken)
        {
            var map = new Dictionary<string, AssignableUserDto>(StringComparer.Ordinal);
            foreach (var id in userIds)
            {
                if (id == "author-2")
                {
                    map[id] = new AssignableUserDto(id, "Ada Author", "ada@example.com");
                }
            }

            return Task.FromResult<IReadOnlyDictionary<string, AssignableUserDto>>(map);
        }
    }

    private sealed class FakeQuestionBankReader : IQuestionBankReader
    {
        public Task<QuestionBankItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult<QuestionBankItemDto?>(null);
    }
}
