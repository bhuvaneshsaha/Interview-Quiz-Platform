using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Users;
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

    [Fact]
    public async Task CreateAsync_rejects_unknown_user()
    {
        var service = new OpeningService(
            new FakeOpeningRepository(),
            _clock,
            NullLogger<OpeningService>.Instance,
            new KnownUsers("user-owner"));

        var request = ValidCreate(owner: "missing-user");
        request.Handlers = ["user-owner"];

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(request, CancellationToken.None));

        Assert.Equal(OpeningService.OwnerNotEmployeeMessage, ex.Message);
    }

    [Fact]
    public async Task CreateAsync_rejects_non_employee_handler()
    {
        var service = new OpeningService(
            new FakeOpeningRepository(),
            _clock,
            NullLogger<OpeningService>.Instance,
            new KnownUsers("user-owner"));
        var request = ValidCreate(owner: "user-owner");
        request.Handlers = ["user-owner", "candidate-user"];

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(request, CancellationToken.None));

        Assert.Equal(OpeningService.HandlersNotEmployeeMessage, ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_rejects_non_employee_owner_and_handler()
    {
        var service = new OpeningService(
            new FakeOpeningRepository(),
            _clock,
            NullLogger<OpeningService>.Instance,
            new KnownUsers("user-owner"));
        var created = await service.CreateAsync(ValidCreate(owner: "user-owner"), CancellationToken.None);
        var request = new UpdateOpeningRequest
        {
            Id = created.Id,
            Title = created.Title,
            JobDescription = created.JobDescription,
            Owner = "candidate-user",
            StartDate = created.StartDate,
            ExpectedCloseDate = created.ExpectedCloseDate,
            Headcount = created.Headcount,
            ExpectedExperienceYears = created.ExpectedExperienceYears,
            Handlers = ["missing-handler"],
            Tags = created.Tags.ToDictionary(pair => pair.Key, pair => pair.Value),
            RowVersion = created.RowVersion
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateAsync(request, CancellationToken.None));

        Assert.Equal(
            $"{OpeningService.OwnerNotEmployeeMessage} {OpeningService.HandlersNotEmployeeMessage}",
            ex.Message);
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
            Handlers = [owner],
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
            if (criteria.OwnerUserIds is { Count: > 0 } ids)
            {
                query = query.Where(o => ids.Contains(o.Owner));
            }
            else if (!string.IsNullOrWhiteSpace(criteria.Owner))
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

    private sealed class KnownUsers : IAssignableUserLookup
    {
        private readonly HashSet<string> _ids;

        public KnownUsers(params string[] ids) => _ids = ids.ToHashSet(StringComparer.Ordinal);

        public Task<IReadOnlyList<AssignableUserDto>> SearchAsync(
            string? query,
            int limit,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AssignableUserDto>>(
                _ids.Where(id => query is null || id.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(limit)
                    .Select(id => new AssignableUserDto(id, id, id + "@example.com"))
                    .ToArray());

        public Task<IReadOnlyDictionary<string, AssignableUserDto>> GetByIdsAsync(
            IReadOnlyCollection<string> userIds,
            CancellationToken cancellationToken)
        {
            var map = userIds
                .Where(id => _ids.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(id => id, id => new AssignableUserDto(id, id, id + "@example.com"), StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyDictionary<string, AssignableUserDto>>(map);
        }
    }
}
