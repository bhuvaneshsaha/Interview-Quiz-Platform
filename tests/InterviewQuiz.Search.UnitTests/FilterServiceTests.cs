using System.Text.Json;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Search.Application;
using InterviewQuiz.Search.Application.Contracts;
using InterviewQuiz.Search.Application.Services;
using InterviewQuiz.Search.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewQuiz.Search.UnitTests;

public sealed class FilterServiceTests
{
    private const string Owner = "owner-1";
    private const string Other = "other-1";
    private const string Listed = "listed-1";
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Visibility_matrix()
    {
        var repo = new FakeFilterRepository();
        var service = new FilterService(repo, _clock, NullLogger<FilterService>.Instance);
        var created = await service.CreateAsync(Owner, QuizzesFilter("Private backend"), CancellationToken.None);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.GetAsync(Other, created.Id, CancellationToken.None));

        await service.ShareAsync(
            Owner,
            created.Id,
            new ShareFilterRequest { ShareMode = FilterShareMode.PublicInsideCompany },
            CancellationToken.None);
        var publicView = await service.GetAsync(Other, created.Id, CancellationToken.None);
        Assert.Equal(FilterShareMode.PublicInsideCompany, publicView.ShareMode);

        await service.UnshareAsync(Owner, created.Id, CancellationToken.None);
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.GetAsync(Other, created.Id, CancellationToken.None));

        await service.ShareAsync(
            Owner,
            created.Id,
            new ShareFilterRequest
            {
                ShareMode = FilterShareMode.SpecificUsers,
                UserIds = [Listed]
            },
            CancellationToken.None);

        var listedView = await service.GetAsync(Listed, created.Id, CancellationToken.None);
        Assert.Equal(FilterShareMode.SpecificUsers, listedView.ShareMode);
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.GetAsync(Other, created.Id, CancellationToken.None));

        var ownerView = await service.GetAsync(Owner, created.Id, CancellationToken.None);
        Assert.Equal(Owner, ownerView.OwnerUserId);
    }

    [Fact]
    public async Task Non_owner_cannot_update_a_visible_public_filter()
    {
        var repo = new FakeFilterRepository();
        var service = new FilterService(repo, _clock, NullLogger<FilterService>.Instance);
        var created = await service.CreateAsync(Owner, QuizzesFilter("Public"), CancellationToken.None);
        await service.ShareAsync(
            Owner,
            created.Id,
            new ShareFilterRequest { ShareMode = FilterShareMode.PublicInsideCompany },
            CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateAsync(
                Other,
                created.Id,
                new UpdateFilterRequest
                {
                    Name = "Hijack",
                    Target = FilterTarget.Quizzes,
                    Criteria = JsonDocument.Parse("""{"keyword":"Hijack"}""").RootElement
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejects_criteria_that_do_not_match_target()
    {
        var service = new FilterService(
            new FakeFilterRepository(),
            _clock,
            NullLogger<FilterService>.Instance);

        var wrongShape = new CreateFilterRequest
        {
            Name = "Wrong",
            Target = FilterTarget.Quizzes,
            Criteria = JsonDocument.Parse("""{"owner":"a@example.com","startDateFrom":"2026-10-01"}""").RootElement
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(Owner, wrongShape, CancellationToken.None));
        Assert.Contains("quizzes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_rejects_non_object_criteria()
    {
        var service = new FilterService(
            new FakeFilterRepository(),
            _clock,
            NullLogger<FilterService>.Instance);

        var request = new CreateFilterRequest
        {
            Name = "Array",
            Target = FilterTarget.Templates,
            Criteria = JsonDocument.Parse("""["keyword"]""").RootElement
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(Owner, request, CancellationToken.None));
        Assert.Equal("Criteria must be a JSON object.", ex.Message);
    }

    [Fact]
    public async Task Create_accepts_quiz_criteria_shape()
    {
        var service = new FilterService(
            new FakeFilterRepository(),
            _clock,
            NullLogger<FilterService>.Instance);

        var created = await service.CreateAsync(
            Owner,
            QuizzesFilter("Ok"),
            CancellationToken.None);

        Assert.Equal(FilterShareMode.Private, created.ShareMode);
        Assert.Equal(FilterTarget.Quizzes, created.Target);
        Assert.Equal("Backend", created.Criteria.GetProperty("keyword").GetString());
    }

    private static CreateFilterRequest QuizzesFilter(string name)
        => new()
        {
            Name = name,
            Target = FilterTarget.Quizzes,
            Criteria = JsonDocument.Parse("""{"keyword":"Backend","experienceMinYears":3}""").RootElement
        };

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class FakeFilterRepository : IFilterRepository
    {
        public List<SavedFilter> Items { get; } = [];

        public Task<SavedFilter?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(f => f.Id == id));

        public Task AddAsync(SavedFilter filter, CancellationToken cancellationToken)
        {
            Items.Add(filter);
            return Task.CompletedTask;
        }

        public void Remove(SavedFilter filter) => Items.Remove(filter);

        public Task<PagedResult<SavedFilter>> ListVisibleAsync(
            string userId,
            FilterTarget? target,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            IEnumerable<SavedFilter> query = Items.Where(f => f.IsVisibleTo(userId));
            if (target is { } filterTarget)
            {
                query = query.Where(f => f.Target == filterTarget);
            }

            var materialized = query.OrderByDescending(f => f.UpdatedAtUtc).ThenBy(f => f.Id).ToList();
            var slice = materialized.Skip(page.Skip).Take(page.PageSize).ToList();
            return Task.FromResult(new PagedResult<SavedFilter>(slice, page.Page, page.PageSize, materialized.Count));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
