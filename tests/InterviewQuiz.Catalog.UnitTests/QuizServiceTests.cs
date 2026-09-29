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
        var service = new QuizService(repo, new FakeOpeningLookup(OpeningId), _clock, NullLogger<QuizService>.Instance);

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
        var service = new QuizService(repo, new FakeOpeningLookup(OpeningId), _clock, NullLogger<QuizService>.Instance);
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
        var service = new QuizService(repo, new FakeOpeningLookup(OpeningId), _clock, NullLogger<QuizService>.Instance);
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

    private static CreateQuizRequest ValidCreate(bool withQuestion = false)
        => new()
        {
            OpeningId = OpeningId,
            Title = "Backend working copy",
            Description = "Authoring sample",
            ExpectedExperienceYears = 5,
            Tags = new Dictionary<string, string> { ["Role"] = "Backend" },
            Questions = withQuestion
                ?
                [
                    new QuestionRequest
                    {
                        Type = QuestionType.TrueFalse,
                        Stem = "A quiz belongs to one opening.",
                        ScoringMode = ScoringMode.Auto,
                        Points = 1,
                        Body = JsonDocument.Parse("""{ "correct": true }""").RootElement
                    }
                ]
                : []
        };

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
