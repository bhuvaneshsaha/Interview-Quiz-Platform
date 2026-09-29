using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Application.Services;
using InterviewQuiz.Openings.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Openings.UnitTests;

public sealed class OpeningServiceTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 29, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task CreateAsync_persists_opening()
    {
        var repo = new FakeOpeningRepository();
        var service = new OpeningService(repo, _clock, NullLogger<OpeningService>.Instance);

        var result = await service.CreateAsync(ValidCreate(), CancellationToken.None);

        Assert.Equal("Platform engineer", result.Title);
        Assert.Single(repo.Items);
        Assert.Equal(result.Id, repo.Items[0].Id);
    }

    [Fact]
    public async Task GetAsync_throws_when_missing()
    {
        var service = new OpeningService(new FakeOpeningRepository(), _clock, NullLogger<OpeningService>.Instance);
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_throws_on_stale_row_version()
    {
        var repo = new FakeOpeningRepository();
        var service = new OpeningService(repo, _clock, NullLogger<OpeningService>.Instance);
        var created = await service.CreateAsync(ValidCreate(), CancellationToken.None);

        var update = new UpdateOpeningRequest
        {
            Id = created.Id,
            Title = created.Title,
            JobDescription = created.JobDescription,
            Owner = created.Owner,
            StartDate = created.StartDate,
            ExpectedCloseDate = created.ExpectedCloseDate,
            Headcount = created.Headcount,
            ExpectedExperienceYears = created.ExpectedExperienceYears,
            Handlers = created.Handlers.ToList(),
            Tags = created.Tags.ToDictionary(p => p.Key, p => p.Value),
            RowVersion = 99
        };

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.UpdateAsync(update, CancellationToken.None));
    }

    [Fact]
    public async Task GetOpeningAsync_returns_lookup_for_other_modules()
    {
        var repo = new FakeOpeningRepository();
        var service = new OpeningService(repo, _clock, NullLogger<OpeningService>.Instance);
        var created = await service.CreateAsync(ValidCreate(), CancellationToken.None);

        var lookup = await service.GetOpeningAsync(created.Id, CancellationToken.None);

        Assert.NotNull(lookup);
        Assert.Equal(created.Title, lookup!.Title);
        Assert.Equal(created.Owner, lookup.Owner);
    }

    [Fact]
    public async Task ListAsync_filters_by_owner()
    {
        var repo = new FakeOpeningRepository();
        var service = new OpeningService(repo, _clock, NullLogger<OpeningService>.Instance);
        await service.CreateAsync(ValidCreate(owner: "a@example.com", title: "A"), CancellationToken.None);
        await service.CreateAsync(ValidCreate(owner: "b@example.com", title: "B"), CancellationToken.None);

        var page = await service.ListAsync(
            new OpeningListCriteria { Owner = "a@example.com" },
            new PageRequest(1, 20),
            CancellationToken.None);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("A", page.Items[0].Title);
    }

    private static CreateOpeningRequest ValidCreate(string owner = "owner@example.com", string title = "Platform engineer")
        => new()
        {
            Title = title,
            JobDescription = "Own the API.",
            Owner = owner,
            StartDate = new DateOnly(2026, 10, 1),
            ExpectedCloseDate = new DateOnly(2026, 12, 1),
            Headcount = 1,
            ExpectedExperienceYears = 4,
            Handlers = ["owner@example.com"],
            Tags = new Dictionary<string, string> { ["Client"] = "Internal" }
        };

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class FakeOpeningRepository : IOpeningRepository
    {
        public List<Opening> Items { get; } = [];

        public Task<Opening?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(o => o.Id == id));

        public Task AddAsync(Opening opening, CancellationToken cancellationToken)
        {
            Items.Add(opening);
            return Task.CompletedTask;
        }

        public void SetExpectedRowVersion(Opening opening, uint expectedRowVersion)
        {
        }

        public Task<PagedResult<Opening>> ListAsync(
            OpeningListCriteria criteria,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            IEnumerable<Opening> query = Items;
            if (!string.IsNullOrWhiteSpace(criteria.Owner))
            {
                query = query.Where(o => o.Owner.Equals(criteria.Owner, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = query
                .OrderByDescending(o => o.StartDate)
                .ThenBy(o => o.Id)
                .ToList();
            var slice = materialized.Skip(page.Skip).Take(page.PageSize).ToList();
            return Task.FromResult(new PagedResult<Opening>(slice, page.Page, page.PageSize, materialized.Count));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
