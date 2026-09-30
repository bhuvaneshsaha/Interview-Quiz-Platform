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

public sealed class QuizServiceTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 29, 8, 0, 0, TimeSpan.Zero));
    private static readonly Guid OpeningId = Guid.Parse("3a7c1f10-6b2d-4c9a-9e11-0f8c2b6a1001");

    [Fact]
    public async Task CreateAsync_persists_quiz_when_opening_exists()
    {
        var repo = new FakeQuizRepository();
        var service = new QuizService(repo, new FakeOpeningLookup(OpeningId), new FakeQuestionBankReader(), _clock, NullLogger<QuizService>.Instance);

        var result = await service.CreateAsync(ValidCreate(), CancellationToken.None);

        Assert.Equal("Backend working copy", result.Title);
        Assert.Single(repo.Items);
        Assert.Equal(OpeningId, result.OpeningId);
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_opening()
    {
        var service = new QuizService(
            new FakeQuizRepository(),
            new FakeOpeningLookup(null),
            new FakeQuestionBankReader(),
            _clock,
            NullLogger<QuizService>.Instance);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(ValidCreate(), CancellationToken.None));

        Assert.Equal("Opening does not exist.", ex.Message);
    }

    [Fact]
    public async Task GetSnapshotAsync_returns_immutable_copy()
    {
        var repo = new FakeQuizRepository();
        var service = new QuizService(repo, new FakeOpeningLookup(OpeningId), new FakeQuestionBankReader(), _clock, NullLogger<QuizService>.Instance);
        var created = await service.CreateAsync(ValidCreate(withQuestion: true), CancellationToken.None);

        var snapshot = await service.GetSnapshotAsync(created.Id, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(created.Id, snapshot!.QuizId);
        Assert.Single(snapshot.Questions);
        Assert.Equal(QuestionType.TrueFalse, snapshot.Questions[0].Type);
    }

    [Fact]
    public async Task UpdateAsync_throws_on_stale_row_version()
    {
        var repo = new FakeQuizRepository();
        var service = new QuizService(repo, new FakeOpeningLookup(OpeningId), new FakeQuestionBankReader(), _clock, NullLogger<QuizService>.Instance);
        var created = await service.CreateAsync(ValidCreate(), CancellationToken.None);

        var update = new UpdateQuizRequest
        {
            Id = created.Id,
            OpeningId = created.OpeningId,
            Title = "Updated",
            Description = created.Description,
            ExpectedExperienceYears = created.ExpectedExperienceYears,
            Tags = created.Tags.ToDictionary(p => p.Key, p => p.Value),
            Questions = [],
            RowVersion = 99
        };

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.UpdateAsync(update, CancellationToken.None));
    }

    [Fact]
    public async Task IncludeQuestionsAsync_copies_new_ids_and_sets_sourceQuestionId()
    {
        var bankId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var bank = new FakeQuestionBankReader();
        bank.Items.Add(BankItem(bankId, "Stem from bank"));
        var repo = new FakeQuizRepository();
        var service = CreateService(repo, bank);
        var created = await service.CreateAsync(ValidCreate(withQuestion: true), CancellationToken.None);
        var originalId = created.Questions.Single().Id;

        var included = await service.IncludeQuestionsAsync(
            created.Id,
            new IncludeQuestionsRequest { QuestionIds = [bankId], RowVersion = created.RowVersion },
            CancellationToken.None);

        Assert.Equal(2, included.Questions.Count);
        var copy = included.Questions.Single(q => q.Id != originalId);
        Assert.NotEqual(bankId, copy.Id);
        Assert.Equal(bankId, copy.SourceQuestionId);
        Assert.Equal("Stem from bank", copy.Stem);
        Assert.Equal(1u, included.RowVersion);
    }

    [Fact]
    public async Task IncludeQuestionsAsync_rejects_archived()
    {
        var bankId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        var bank = new FakeQuestionBankReader();
        bank.Items.Add(BankItem(bankId, "Archived stem", archivedAtUtc: _clock.UtcNow));
        var repo = new FakeQuizRepository();
        var service = CreateService(repo, bank);
        var created = await service.CreateAsync(ValidCreate(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.IncludeQuestionsAsync(
                created.Id,
                new IncludeQuestionsRequest { QuestionIds = [bankId], RowVersion = created.RowVersion },
                CancellationToken.None));

        Assert.Equal("Archived question cannot be included.", ex.Message);
        Assert.Empty(repo.Items.Single().Questions);
    }

    [Fact]
    public async Task IncludeQuestionsAsync_inserts_at_index_in_request_order()
    {
        var firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var bank = new FakeQuestionBankReader();
        bank.Items.Add(BankItem(firstId, "First bank"));
        bank.Items.Add(BankItem(secondId, "Second bank"));
        var repo = new FakeQuizRepository();
        var service = CreateService(repo, bank);
        var created = await service.CreateAsync(
            ValidCreate(withQuestion: true, extraQuestion: true),
            CancellationToken.None);

        Assert.Equal(2, created.Questions.Count);
        var originalFirst = created.Questions[0].Id;
        var originalSecond = created.Questions[1].Id;

        var included = await service.IncludeQuestionsAsync(
            created.Id,
            new IncludeQuestionsRequest
            {
                QuestionIds = [firstId, secondId],
                InsertAt = 1,
                RowVersion = created.RowVersion
            },
            CancellationToken.None);

        Assert.Equal(
            [originalFirst, firstId, secondId, originalSecond],
            included.Questions.Select(q => q.Id == originalFirst || q.Id == originalSecond ? q.Id : q.SourceQuestionId).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3 }, included.Questions.Select(q => q.SortOrder).ToArray());
        Assert.Equal("First bank", included.Questions[1].Stem);
        Assert.Equal("Second bank", included.Questions[2].Stem);
    }

    [Fact]
    public async Task IncludeQuestionsAsync_throws_on_stale_row_version()
    {
        var bankId = Guid.Parse("cccccccc-dddd-eeee-ffff-aaaaaaaaaaaa");
        var bank = new FakeQuestionBankReader();
        bank.Items.Add(BankItem(bankId, "Bank stem"));
        var repo = new FakeQuizRepository();
        var service = CreateService(repo, bank);
        var created = await service.CreateAsync(ValidCreate(), CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.IncludeQuestionsAsync(
                created.Id,
                new IncludeQuestionsRequest { QuestionIds = [bankId], RowVersion = 99 },
                CancellationToken.None));
    }

    private QuizService CreateService(FakeQuizRepository repo, FakeQuestionBankReader bank)
        => new(repo, new FakeOpeningLookup(OpeningId), bank, _clock, NullLogger<QuizService>.Instance);

    private static QuestionBankItemDto BankItem(Guid id, string stem, DateTimeOffset? archivedAtUtc = null)
        => new()
        {
            Id = id,
            SortOrder = 0,
            Type = QuestionType.TrueFalse,
            Stem = stem,
            ScoringMode = ScoringMode.Auto,
            Points = 1,
            Body = JsonSerializer.SerializeToElement(new { correct = true }, CatalogJson.SerializerOptions),
            Title = "Library item",
            Tags = new Dictionary<string, string>(),
            ExpectedExperienceYears = 0,
            ArchivedAtUtc = archivedAtUtc
        };

    private static CreateQuizRequest ValidCreate(bool withQuestion = false, bool extraQuestion = false)
        => new()
        {
            OpeningId = OpeningId,
            Title = "Backend working copy",
            Description = "Authoring sample",
            ExpectedExperienceYears = 5,
            Tags = new Dictionary<string, string> { ["Role"] = "Backend" },
            Questions = withQuestion
                ? extraQuestion
                    ?
                    [
                        Tf("A quiz belongs to one opening."),
                        Tf("Templates copy questions.")
                    ]
                    :
                    [
                        Tf("A quiz belongs to one opening.")
                    ]
                : []
        };

    private static QuestionRequest Tf(string stem)
        => new()
        {
            Type = QuestionType.TrueFalse,
            Stem = stem,
            ScoringMode = ScoringMode.Auto,
            Points = 1,
            Body = JsonDocument.Parse("""{ "correct": true }""").RootElement
        };

    private sealed class FakeQuestionBankReader : IQuestionBankReader
    {
        public List<QuestionBankItemDto> Items { get; } = [];

        public Task<QuestionBankItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));
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
            IEnumerable<Quiz> query = Items;
            if (criteria.OpeningId is { } openingId)
            {
                query = query.Where(q => q.OpeningId == openingId);
            }

            var materialized = query.OrderByDescending(q => q.UpdatedAtUtc).ThenBy(q => q.Id).ToList();
            var slice = materialized.Skip(page.Skip).Take(page.PageSize).ToList();
            return Task.FromResult(new PagedResult<Quiz>(slice, page.Page, page.PageSize, materialized.Count));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
