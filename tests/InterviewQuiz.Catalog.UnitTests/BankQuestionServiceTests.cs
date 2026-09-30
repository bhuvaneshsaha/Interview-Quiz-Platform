using System.Text.Json;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Catalog.Application.Contracts;
using InterviewQuiz.Catalog.Application.Services;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Catalog.UnitTests;

public sealed class BankQuestionServiceTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 30, 11, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task CreateAsync_rejects_invalid_body()
    {
        var service = new BankQuestionService(
            new FakeBankQuestionRepository(),
            _clock,
            NullLogger<BankQuestionService>.Instance);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(InvalidMcCreate(), CancellationToken.None));

        Assert.Contains("exactly one correct", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_persists_live_item()
    {
        var repo = new FakeBankQuestionRepository();
        var service = new BankQuestionService(repo, _clock, NullLogger<BankQuestionService>.Instance);

        var created = await service.CreateAsync(ValidTfCreate(), CancellationToken.None);

        Assert.Equal("One opening", created.Title);
        Assert.Null(created.ArchivedAtUtc);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task UpdateAsync_throws_on_stale_row_version()
    {
        var repo = new FakeBankQuestionRepository();
        var service = new BankQuestionService(repo, _clock, NullLogger<BankQuestionService>.Instance);
        var created = await service.CreateAsync(ValidTfCreate(), CancellationToken.None);

        var update = ValidTfUpdate(rowVersion: 99, title: "Updated");
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.UpdateAsync(created.Id, update, CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_archived_without_write_is_forbidden()
    {
        var service = new BankQuestionService(
            new FakeBankQuestionRepository(),
            _clock,
            NullLogger<BankQuestionService>.Instance);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ListAsync(
                new BankQuestionListCriteria { Archived = true },
                new PageRequest(1, 20),
                canListArchived: false,
                CancellationToken.None));
    }

    [Fact]
    public async Task ArchiveAsync_then_unarchive()
    {
        var repo = new FakeBankQuestionRepository();
        var service = new BankQuestionService(repo, _clock, NullLogger<BankQuestionService>.Instance);
        var created = await service.CreateAsync(ValidTfCreate(), CancellationToken.None);

        var archived = await service.ArchiveAsync(created.Id, CancellationToken.None);
        Assert.NotNull(archived.ArchivedAtUtc);

        var live = await service.UnarchiveAsync(created.Id, CancellationToken.None);
        Assert.Null(live.ArchivedAtUtc);
    }

    private static CreateBankQuestionRequest ValidTfCreate()
        => new()
        {
            Title = "One opening",
            Tags = new Dictionary<string, string> { ["Role"] = "Backend" },
            ExpectedExperienceYears = 0,
            Type = QuestionType.TrueFalse,
            Stem = "A quiz belongs to one opening.",
            ScoringMode = ScoringMode.Auto,
            Points = 1,
            Body = JsonSerializer.SerializeToElement(new { correct = true }, CatalogJson.SerializerOptions)
        };

    private static CreateBankQuestionRequest InvalidMcCreate()
        => new()
        {
            Title = "Bad MCQ",
            Type = QuestionType.MultipleChoiceSingle,
            Stem = "Pick one",
            ScoringMode = ScoringMode.Auto,
            Points = 1,
            Body = JsonSerializer.SerializeToElement(new
            {
                options = new[]
                {
                    new { id = "a", text = "One", isCorrect = false },
                    new { id = "b", text = "Two", isCorrect = false }
                }
            }, CatalogJson.SerializerOptions)
        };

    private static UpdateBankQuestionRequest ValidTfUpdate(uint rowVersion, string title)
        => new()
        {
            Title = title,
            Tags = new Dictionary<string, string> { ["Role"] = "Backend" },
            ExpectedExperienceYears = 0,
            Type = QuestionType.TrueFalse,
            Stem = "A quiz belongs to one opening.",
            ScoringMode = ScoringMode.Auto,
            Points = 1,
            Body = JsonSerializer.SerializeToElement(new { correct = true }, CatalogJson.SerializerOptions),
            RowVersion = rowVersion
        };

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class FakeBankQuestionRepository : IBankQuestionRepository
    {
        public List<BankQuestion> Items { get; } = [];

        public Task<BankQuestion?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(q => q.Id == id));

        public Task AddAsync(BankQuestion question, CancellationToken cancellationToken)
        {
            Items.Add(question);
            return Task.CompletedTask;
        }

        public void SetExpectedRowVersion(BankQuestion question, uint expectedRowVersion)
        {
        }

        public Task<PagedResult<BankQuestion>> ListAsync(
            BankQuestionListCriteria criteria,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            IEnumerable<BankQuestion> query = Items;
            query = criteria.Archived
                ? query.Where(q => q.ArchivedAtUtc is not null)
                : query.Where(q => q.ArchivedAtUtc is null);

            var materialized = query.OrderByDescending(q => q.UpdatedAtUtc).ThenBy(q => q.Id).ToList();
            var slice = materialized.Skip(page.Skip).Take(page.PageSize).ToList();
            return Task.FromResult(new PagedResult<BankQuestion>(slice, page.Page, page.PageSize, materialized.Count));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
