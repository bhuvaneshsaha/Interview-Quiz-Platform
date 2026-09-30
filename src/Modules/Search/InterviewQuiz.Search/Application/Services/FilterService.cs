using System.Diagnostics;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Search.Application.Contracts;
using InterviewQuiz.Search.Domain;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Search.Application.Services;

public sealed class FilterService : IFilterService
{
    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Search");

    private readonly IFilterRepository _filters;
    private readonly IClock _clock;
    private readonly ILogger<FilterService> _logger;

    public FilterService(IFilterRepository filters, IClock clock, ILogger<FilterService> logger)
    {
        _filters = filters;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PagedResult<FilterResponse>> ListAsync(
        string userId,
        FilterTarget? target,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("filters.list");
        RequireUser(userId);
        var result = await _filters.ListVisibleAsync(userId, target, page, cancellationToken);
        var items = result.Items.Select(Map).ToList();
        return new PagedResult<FilterResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<FilterResponse> GetAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("filters.list");
        var filter = await GetVisibleAsync(userId, id, cancellationToken);
        return Map(filter);
    }

    public async Task<FilterResponse> CreateAsync(
        string userId,
        CreateFilterRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("filters.write");
        RequireUser(userId);
        if (request.Target is null)
        {
            throw new DomainException("Target is required.");
        }

        var criteria = FilterCriteriaParser.Canonicalize(request.Target.Value, request.Criteria);
        var filter = SavedFilter.Create(userId, request.Name, request.Target.Value, criteria, _clock);
        await _filters.AddAsync(filter, cancellationToken);
        await _filters.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created filter {FilterId} for target {Target}",
            filter.Id,
            JsonCamel(filter.Target));

        return Map(filter);
    }

    public async Task<FilterResponse> UpdateAsync(
        string userId,
        Guid id,
        UpdateFilterRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("filters.write");
        var filter = await GetOwnedAsync(userId, id, cancellationToken);
        if (request.Target is null)
        {
            throw new DomainException("Target is required.");
        }

        var criteria = FilterCriteriaParser.Canonicalize(request.Target.Value, request.Criteria);
        filter.Update(request.Name, request.Target.Value, criteria, _clock);
        await _filters.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated filter {FilterId}", filter.Id);
        return Map(filter);
    }

    public async Task DeleteAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("filters.write");
        var filter = await GetOwnedAsync(userId, id, cancellationToken);
        _filters.Remove(filter);
        await _filters.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted filter {FilterId}", filter.Id);
    }

    public async Task<FilterResponse> ShareAsync(
        string userId,
        Guid id,
        ShareFilterRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("filters.share");
        var filter = await GetOwnedAsync(userId, id, cancellationToken);
        if (request.ShareMode is null)
        {
            throw new DomainException("Share mode is required.");
        }

        filter.Share(request.ShareMode.Value, request.UserIds, _clock);
        await _filters.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Shared filter {FilterId} as {ShareMode}",
            filter.Id,
            JsonCamel(filter.ShareMode));

        return Map(filter);
    }

    public async Task<FilterResponse> UnshareAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("filters.share");
        var filter = await GetOwnedAsync(userId, id, cancellationToken);
        filter.Unshare(_clock);
        await _filters.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unshared filter {FilterId}", filter.Id);
        return Map(filter);
    }

    private async Task<SavedFilter> GetVisibleAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        RequireUser(userId);
        var filter = await _filters.GetAsync(id, cancellationToken);
        if (filter is null || !filter.IsVisibleTo(userId))
        {
            throw new EntityNotFoundException(nameof(SavedFilter), id);
        }

        return filter;
    }

    private async Task<SavedFilter> GetOwnedAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        var filter = await GetVisibleAsync(userId, id, cancellationToken);
        if (!filter.IsOwnedBy(userId))
        {
            throw new ForbiddenException("Only the owner can change this filter.");
        }

        return filter;
    }

    private static void RequireUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("User id is required.");
        }
    }

    internal static FilterResponse Map(SavedFilter filter)
        => new()
        {
            Id = filter.Id,
            Name = filter.Name,
            Target = filter.Target,
            Criteria = filter.Criteria.RootElement.Clone(),
            OwnerUserId = filter.OwnerUserId,
            ShareMode = filter.ShareMode,
            SharedWithUserIds = filter.SharedWithUserIds.ToArray(),
            CreatedAtUtc = filter.CreatedAtUtc,
            UpdatedAtUtc = filter.UpdatedAtUtc
        };

    private static string JsonCamel(Enum value)
        => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
