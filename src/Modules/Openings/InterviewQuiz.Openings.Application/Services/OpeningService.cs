using System.Diagnostics;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Kernel.Users;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Domain;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Openings.Application.Services;

public sealed class OpeningService : IOpeningService, IOpeningLookup
{
    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Openings");

    private readonly IOpeningRepository _openings;
    private readonly IAssignableUserLookup _users;
    private readonly IClock _clock;
    private readonly ILogger<OpeningService> _logger;

    public OpeningService(
        IOpeningRepository openings,
        IClock clock,
        ILogger<OpeningService> logger,
        IAssignableUserLookup? users = null)
    {
        _openings = openings;
        _users = users ?? AcceptingUserLookup.Instance;
        _clock = clock;
        _logger = logger;
    }

    public async Task<OpeningResponse> CreateAsync(CreateOpeningRequest request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("openings.create");
        await EnsurePeopleExistAsync(request.Owner, request.Handlers, cancellationToken);
        var opening = Opening.Create(
            request.Title,
            request.JobDescription,
            request.Owner,
            request.StartDate,
            request.ExpectedCloseDate,
            request.Headcount,
            request.ExpectedExperienceYears,
            request.Handlers,
            request.Tags,
            _clock);

        await _openings.AddAsync(opening, cancellationToken);
        await _openings.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created opening {OpeningId}", opening.Id);
        return await MapAsync(opening, cancellationToken);
    }

    public async Task<OpeningResponse> UpdateAsync(UpdateOpeningRequest request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("openings.update");
        var opening = await _openings.GetAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Opening), request.Id);

        if (opening.RowVersion != request.RowVersion)
        {
            throw new ConcurrencyException("Opening was modified by another request. Reload and retry.");
        }

        await EnsurePeopleExistAsync(request.Owner, request.Handlers, cancellationToken);
        opening.Update(
            request.Title,
            request.JobDescription,
            request.Owner,
            request.StartDate,
            request.ExpectedCloseDate,
            request.Headcount,
            request.ExpectedExperienceYears,
            request.Handlers,
            request.Tags,
            _clock);

        _openings.SetExpectedRowVersion(opening, request.RowVersion);
        await _openings.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated opening {OpeningId}", opening.Id);
        return await MapAsync(opening, cancellationToken);
    }

    public async Task<OpeningResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("openings.get");
        var opening = await _openings.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Opening), id);

        return await MapAsync(opening, cancellationToken);
    }

    public async Task<OpeningLookupDto?> GetOpeningAsync(Guid id, CancellationToken cancellationToken)
    {
        var opening = await _openings.GetAsync(id, cancellationToken);
        if (opening is null)
        {
            return null;
        }

        return new OpeningLookupDto(
            opening.Id,
            opening.Title,
            opening.Owner,
            opening.StartDate,
            opening.ExpectedCloseDate,
            opening.Headcount,
            opening.ExpectedExperienceYears);
    }

    public async Task<PagedResult<OpeningResponse>> ListAsync(
        OpeningListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("openings.list");
        if (!string.IsNullOrWhiteSpace(criteria.Owner))
        {
            var matches = await _users.SearchAsync(criteria.Owner, 20, cancellationToken);
            criteria.OwnerUserIds = matches.Select(user => user.Id).ToArray();
        }

        var result = await _openings.ListAsync(criteria, page, cancellationToken);
        var people = await LoadPeopleAsync(result.Items, cancellationToken);
        var items = result.Items.Select(opening => Map(opening, people)).ToList();
        return new PagedResult<OpeningResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    private async Task EnsurePeopleExistAsync(
        string? owner,
        IEnumerable<string>? handlers,
        CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(owner))
        {
            ids.Add(owner.Trim());
        }

        foreach (var handler in handlers ?? [])
        {
            if (!string.IsNullOrWhiteSpace(handler))
            {
                ids.Add(handler.Trim());
            }
        }

        if (ids.Count == 0)
        {
            return;
        }

        var known = await _users.GetByIdsAsync(ids, cancellationToken);
        var missing = ids.Where(id => !known.ContainsKey(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
        {
            throw new DomainException("Unknown user.");
        }
    }

    private async Task<OpeningResponse> MapAsync(Opening opening, CancellationToken cancellationToken)
    {
        var people = await LoadPeopleAsync([opening], cancellationToken);
        return Map(opening, people);
    }

    private async Task<IReadOnlyDictionary<string, AssignableUserDto>> LoadPeopleAsync(
        IEnumerable<Opening> openings,
        CancellationToken cancellationToken)
    {
        var ids = openings
            .SelectMany(opening => opening.Handlers.Append(opening.Owner))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
        return await _users.GetByIdsAsync(ids, cancellationToken);
    }

    internal static OpeningResponse Map(Opening opening, IReadOnlyDictionary<string, AssignableUserDto> people)
    {
        var owner = Person(opening.Owner, people);
        return new OpeningResponse
        {
            Id = opening.Id,
            Title = opening.Title,
            JobDescription = opening.JobDescription,
            Owner = opening.Owner,
            OwnerName = owner.Name,
            OwnerEmail = owner.Email,
            OwnerResolved = owner.Resolved,
            StartDate = opening.StartDate,
            ExpectedCloseDate = opening.ExpectedCloseDate,
            Headcount = opening.Headcount,
            ExpectedExperienceYears = opening.ExpectedExperienceYears,
            Handlers = opening.Handlers,
            HandlerPeople = opening.Handlers.Select(id => Person(id, people)).ToList(),
            Tags = opening.Tags,
            RowVersion = opening.RowVersion,
            CreatedAtUtc = opening.CreatedAtUtc,
            UpdatedAtUtc = opening.UpdatedAtUtc
        };
    }

    private static OpeningPersonResponse Person(string id, IReadOnlyDictionary<string, AssignableUserDto> people)
    {
        if (people.TryGetValue(id, out var user))
        {
            return new OpeningPersonResponse
            {
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email,
                Resolved = true
            };
        }

        return new OpeningPersonResponse
        {
            UserId = "",
            Name = id,
            Email = "",
            Resolved = false
        };
    }

    /// <summary>
    /// Unit tests that do not pass a directory accept every id. Production registers the Access lookup.
    /// </summary>
    private sealed class AcceptingUserLookup : IAssignableUserLookup
    {
        public static readonly AcceptingUserLookup Instance = new();

        public Task<IReadOnlyList<AssignableUserDto>> SearchAsync(
            string? query,
            int limit,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Task.FromResult<IReadOnlyList<AssignableUserDto>>([]);
            }

            var id = query.Trim();
            return Task.FromResult<IReadOnlyList<AssignableUserDto>>([new AssignableUserDto(id, id, "")]);
        }

        public Task<IReadOnlyDictionary<string, AssignableUserDto>> GetByIdsAsync(
            IReadOnlyCollection<string> userIds,
            CancellationToken cancellationToken)
        {
            var map = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(id => id.Trim(), id => new AssignableUserDto(id.Trim(), id.Trim(), ""), StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyDictionary<string, AssignableUserDto>>(map);
        }
    }
}
